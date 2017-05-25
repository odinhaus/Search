using Altus.Suffūz.Serialization.Binary;
using Newtonsoft.Json;
using Common;
using Common.Collections;
using Common.Serialization.Binary;
using Data.Core;
using Data.Core.Auditing;
using Data.Core.Compilation;
using Data.Core.ComponentModel;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Data.Core.Security;

namespace Data
{
    public static class Model
    {
        public static T New<T>(params Type[] additionalInterfaces) where T : IModel
        {
            return RuntimeModelBuilder.CreateModelInstance<T>(additionalInterfaces: additionalInterfaces);
        }

        public static T New<T>(Dictionary<string, object> propertyValues, params Type[] additionalInterfaces) where T : IModel
        {
            return RuntimeModelBuilder.CreateModelInstanceActivator<T>(additionalInterfaces: additionalInterfaces)(propertyValues);
        }

        public static void New<T>(IList<object> propertyValueList, IList<T> targetList, params Type[] additionalInterfaces) where T : IModel
        {
            if (propertyValueList != null)
            {
                foreach (var propertyValue in propertyValueList)
                {
                    targetList.Add(New<T>((Dictionary<string, object>)propertyValue, additionalInterfaces));
                }
            }
        }

        public static T[] NewArray<T>(IList<object> propertyValueList, params Type[] additionalInterfaces) where T : IModel
        {
            var list = new List<T>();
            New<T>(propertyValueList, list, additionalInterfaces);
            return list.ToArray();
        }
    }
}

namespace Data.Core.Compilation
{
    public class RuntimeModelBuilder : IEqualityComparer<PropertyInfo>
    {
        protected static Dictionary<string, Type> _buildCache = new Dictionary<string, Type>();
        protected static AssemblyName _asmName = new AssemblyName() { Name = "Data.Runtime.Types" };
        protected static ModuleBuilder _modBuilder;
        protected static AssemblyBuilder _asmBuilder;
        private static int _propertyNumber;

        static RuntimeModelBuilder()
        {
            _asmBuilder = Thread.GetDomain().DefineDynamicAssembly(_asmName, AssemblyBuilderAccess.RunAndSave);
            _modBuilder = _asmBuilder.DefineDynamicModule(_asmName.Name, _asmName.Name + ".dll", true);
        }

        public static Type CreateModelType<T>(Type baseType = null, bool loadFromCache = true, params Type[] additionalInterfaces) where T : IModel
        {
            return CreateModelType(typeof(T), baseType, loadFromCache, additionalInterfaces);
        }

        public static Type CreateModelType(Type interfaceType, Type baseType = null, bool loadFromCache = true, params Type[] additionalInterfaces)
        {
            if (!interfaceType.IsInterface) throw new InvalidOperationException("The generic parameter type T must be an interface.");
            if (!interfaceType.Implements<IModel>()) throw new InvalidOperationException("The generic parameter type T must derive from IModel.");
            if (additionalInterfaces == null)
            {
                additionalInterfaces = new Type[0];
            }
            foreach(var iface in additionalInterfaces)
            {
                if (!iface.IsInterface) throw new InvalidOperationException("Additional interfaces must all be interface types");
            }

            Type instanceType = null;
            var className = "Dyn_" + interfaceType.FullName.Replace(".", "_");
            lock (_buildCache)
            {
                if (!loadFromCache || !_buildCache.TryGetValue(className, out instanceType))
                {
                    var typeBuilder = _modBuilder.DefineType(className, 
                        TypeAttributes.Public 
                        | TypeAttributes.AutoClass 
                        | TypeAttributes.AnsiClass 
                        | TypeAttributes.Class 
                        | TypeAttributes.Serializable 
                        | TypeAttributes.BeforeFieldInit);

                    typeBuilder.AddInterfaceImplementation(interfaceType);
                    typeBuilder.AddInterfaceImplementation(typeof(IBinarySerializable));
                    typeBuilder.AddInterfaceImplementation(typeof(IAny));

                    var extraIFaces = new List<Type>(additionalInterfaces);
                    if (interfaceType.Implements<ILink>() && ModelTypeConverter.AdditionalLinkInterfaces != null)
                    {
                        extraIFaces.AddRange(ModelTypeConverter.AdditionalLinkInterfaces);
                    }
                    else if (ModelTypeConverter.AdditionalModelInterfaces != null)
                    {
                        extraIFaces.AddRange(ModelTypeConverter.AdditionalModelInterfaces);
                    }

                    foreach (var iface in extraIFaces)
                        typeBuilder.AddInterfaceImplementation(iface);

                    if (!interfaceType.Implements<owns>())
                    {
                        typeBuilder.AddInterfaceImplementation(typeof(IOwnedModel));
                        typeBuilder.AddInterfaceImplementation(typeof(_IOwnedModel));
                        extraIFaces.Add(typeof(_IOwnedModel));
                    }

                    if (baseType == null)
                    {
                        if (interfaceType.Implements<ILink>())
                        {
                            baseType = ModelTypeConverter.LinkBaseType;
                        }
                        else
                        {
                            baseType = ModelTypeConverter.ModelBaseType;
                        }
                    }
                    typeBuilder.SetParent(baseType);
                    List<PropertyInfo> members = GetMembers(interfaceType).OfType<PropertyInfo>().Distinct(new PropertyInfoComparer()).ToList();
                    foreach(var iface in extraIFaces)
                    {
                        members.AddRange(GetMembers(iface).OfType<PropertyInfo>());
                    }

                    var baseMembers = new List<PropertyInfo>();
                    if (baseType != null)
                    {
                        baseMembers.AddRange(baseType.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                                                     .Distinct(new PropertyInfoComparer()).ToList());
                    }

                    var propertyChanged = BuildPropertyChangedEvent(interfaceType, typeBuilder, baseType);
                    var propertyChanging = BuildPropertyChangingEvent(interfaceType, typeBuilder, baseType);

                    var props = new List<PropertyInfo>();
                    var fields = new List<FieldInfo>();
                    _propertyNumber = 0;
                    foreach (var member in members)//.Where(mi => !baseMembers.Any(pi => pi.Name.Equals(mi.Name) && pi.PropertyType.Equals(((PropertyInfo)mi).PropertyType))))
                    {
                        if (member is PropertyInfo)
                        {
                            if (member.Name.Equals("ModelType"))
                            {
                                props.Add(BuildModelTypeProperty(interfaceType,
                                    typeBuilder,
                                    baseType,
                                    (PropertyInfo)member,
                                    propertyChanged, 
                                    propertyChanging));
                            }
                            else if (member.Name.Equals("Owner"))
                            {
                                if (member.CanWrite)
                                {
                                    // owner prop is duplicated, so we only want the Read/Write version, ignore the Read only version
                                    props.Add(BuildOwnerProperty(typeBuilder,
                                        interfaceType,
                                        propertyChanged,
                                        propertyChanging));
                                }
                            }
                            else
                            {
                                FieldInfo fld;
                                props.Add(BuildProperty(interfaceType,
                                    typeBuilder,
                                    baseType,
                                    (PropertyInfo)member,
                                    propertyChanged,
                                    propertyChanging, out fld));
                                if (fld != null)
                                {
                                    fields.Add(fld);
                                    _propertyNumber++;
                                }
                            }
                        }
                    }

                    FieldInfo protoBuffField;
                    var protoBuffProp = BuildProtocolBufferProperty(typeBuilder, interfaceType, out protoBuffField);

                    //var ownerProp = BuildOwnerProperty(typeBuilder, interfaceType, propertyChanged, propertyChanging);

                    var allProps = props.Union(baseMembers).Distinct(new RuntimeModelBuilder())
                                                           .Union(new PropertyInfo[] { protoBuffProp })
                                                           .ToList();
                    //if (ownerProp != null)
                    //{
                    //    allProps.Add(ownerProp);
                    //}

                    var defaultCtor = BuildDefaultCtor(interfaceType, typeBuilder, baseType, fields.Where(pi => pi.FieldType.Implements<IList>()).ToArray(), propertyChanging, protoBuffField);

                    BuildDictionaryCtor(interfaceType, typeBuilder, defaultCtor, allProps.ToArray());
                    
                    BuildGetKeyMethod(interfaceType, typeBuilder, props.SingleOrDefault(p => p.Name.Equals("Key") || p.Name.EndsWith(".Key")));
                    BuildSetKeyMethod(interfaceType, typeBuilder, props.SingleOrDefault(p => p.Name.Equals("Key") || p.Name.EndsWith(".Key")));

                    BuildToBytes(interfaceType, typeBuilder, props, extraIFaces.ToArray());
                    BuildFromBytes(interfaceType, typeBuilder, props, extraIFaces.ToArray());

                    BuildCompareMethod(interfaceType, typeBuilder, props);

                    instanceType = typeBuilder.CreateType();
                    if (_buildCache.ContainsKey(className))
                    {
                        _buildCache[className] = instanceType;
                    }
                    else
                    {
                        _buildCache.Add(className, instanceType);
                    }
                }
                else
                {
                    instanceType = _buildCache[className];
                }
            }
            return instanceType;
        }

        private static PropertyInfo BuildOwnerProperty(TypeBuilder typeBuilder, Type interfaceType, FieldInfo propertyChanged, FieldInfo propertyChanging)
        {
            PropertyInfo prop = null;

            if (!interfaceType.Implements<owns>())
            {
                var explicitProp = typeof(IOwnedModel).GetProperty("Owner");
                var implicitProp = typeof(_IOwnedModel).GetProperty("Owner");
                FieldInfo fld;
                prop = BuildProperty(implicitProp.DeclaringType, typeBuilder, typeBuilder.BaseType, implicitProp, propertyChanged, propertyChanging, out fld);

                // create explicit interface implementation for IOwnedModel
                var property = typeBuilder.DefineProperty(typeof(IOwnedModel).FullName + "." + explicitProp.Name,
                    PropertyAttributes.HasDefault,
                    explicitProp.PropertyType,
                    null);
                var getter = typeBuilder.DefineMethod(typeof(IOwnedModel).FullName + ".get_" + explicitProp.Name,
                    MethodAttributes.Private
                    | MethodAttributes.SpecialName
                    | MethodAttributes.HideBySig
                    | MethodAttributes.NewSlot
                    | MethodAttributes.Final
                    | MethodAttributes.Virtual,
                    explicitProp.PropertyType,
                    Type.EmptyTypes);
                var code = getter.GetILGenerator();
                code.Emit(OpCodes.Ldarg_0);
                code.Emit(OpCodes.Call, prop.GetMethod);
                code.Emit(OpCodes.Ret);

                property.SetGetMethod(getter);
                typeBuilder.DefineMethodOverride(getter, explicitProp.GetMethod);
            }

            return prop;
        }

        private static MethodInfo BuildCompareMethod(Type interfaceType, TypeBuilder typeBuilder, List<PropertyInfo> props)
        {
            // public IEnumerable<AuditedChange> Compare(IModel model, string prefix) {
            var method = typeBuilder.DefineMethod("Compare",
                MethodAttributes.Public
                | MethodAttributes.HideBySig
                | MethodAttributes.NewSlot
                | MethodAttributes.Virtual
                | MethodAttributes.Final,
                CallingConventions.Standard,
                typeof(IEnumerable<AuditedChange>),
                new Type[] { typeof(IModel), typeof(string) });
            var code = method.GetILGenerator();

            var changes = code.DeclareLocal(typeof(List<AuditedChange>));
            var target = code.DeclareLocal(interfaceType);

            // List<AuditedChange> changes = new List<AuditedChange>();
            code.Emit(OpCodes.Newobj, changes.LocalType.GetConstructor(Type.EmptyTypes));
            code.Emit(OpCodes.Stloc, changes);
            // var target = model as IPatient;
            code.Emit(OpCodes.Ldarg_1);
            code.Emit(OpCodes.Isinst, interfaceType);
            code.Emit(OpCodes.Stloc, target);

            foreach(var prop in props)
            {
                if (prop.PropertyType.Implements<IModel>())
                {
                    /* 
                    IL_006e:  ldarg.0
                    IL_006f:  call       instance class [NNHIS.Data]NNHIS.Data.Identification.IAddress Patient::get_HomeAddress()
                    IL_0074:  ldnull
                    IL_0075:  cgt.un
                    IL_0077:  stloc.3
                    IL_0078:  ldloc.3
                    IL_0079:  brfalse    IL_00ff
                    IL_007e:  nop
                    IL_007f:  ldloc.1
                    IL_0080:  brtrue.s   IL_0085
                    IL_0082:  ldnull
                    IL_0083:  br.s       IL_008b
                    IL_0085:  ldloc.1
                    IL_0086:  callvirt   instance class [NNHIS.Data]NNHIS.Data.Identification.IAddress [NNHIS.Data]NNHIS.Data.Identification.Individuals.IPatient::get_HomeAddress()
                    IL_008b:  ldnull
                    IL_008c:  ceq
                    IL_008e:  stloc.s    V_4
                    IL_0090:  ldloc.s    V_4
                    IL_0092:  brfalse.s  IL_00d3
                    IL_0094:  nop
                    IL_0095:  ldloc.0
                    IL_0096:  newobj     instance void [Data.Core]Data.Core.Auditing.AuditedChange::.ctor()
                    IL_009b:  dup
                    IL_009c:  ldarg.2
                    IL_009d:  ldstr      "HomeAddress"
                    IL_00a2:  call       string [mscorlib]System.String::Concat(string, string)
                    IL_00a7:  callvirt   instance void [Data.Core]Data.Core.Auditing.AuditedChange::set_PropertyName(string)
                    IL_00ac:  nop
                    IL_00ad:  dup
                    IL_00ae:  ldc.i4.0
                    IL_00af:  callvirt   instance void [Data.Core]Data.Core.Auditing.AuditedChange::set_ChangeType(valuetype [Data.Core]Data.Core.Auditing.AuditChangeType)
                    IL_00b4:  nop
                    IL_00b5:  dup
                    IL_00b6:  ldc.i4.0
                    IL_00b7:  callvirt   instance void [Data.Core]Data.Core.Auditing.AuditedChange::set_ItemIndex(int32)
                    IL_00bc:  nop
                    IL_00bd:  dup
                    IL_00be:  ldarg.0
                    IL_00bf:  call       instance class [NNHIS.Data]NNHIS.Data.Identification.IAddress Patient::get_HomeAddress()
                    IL_00c4:  callvirt   instance void [Data.Core]Data.Core.Auditing.AuditedChange::set_Value(object)
                    IL_00c9:  nop
                    IL_00ca:  callvirt   instance void class [mscorlib]System.Collections.Generic.List`1<class [Data.Core]Data.Core.Auditing.AuditedChange>::Add(!0)
  
                    */


                    var propIsNotNull = code.DefineLabel();
                    var propIsNull = code.DefineLabel();
                    var targetIsNotNull = code.DefineLabel();
                    var targetIsNotNull2 = code.DefineLabel();
                    var targetIsNotNull3 = code.DefineLabel();
                    var targetIsNotNull4 = code.DefineLabel();
                    var targetIsNull = code.DefineLabel();
                    var targetIsNull2 = code.DefineLabel();
                    var targetIsNull3 = code.DefineLabel();
                    var bothPropsExist = code.DefineLabel();
                    var checkBothProps = code.DefineLabel();
                    var jumpToEnd = code.DefineLabel();

                    //if (HomeAddress == null && patient?.HomeAddress != null)
                    //{
                    //    changes.Add(new AuditedChange() { PropertyName = prefix + "HomeAddress", ChangeType = AuditChangeType.Removed, ItemIndex = 0, Value = NameFirst });
                    //}
                    //else if (HomeAddress != null)
                    //{
                    //    changes.AddRange(HomeAddress.Compare(patient?.HomeAddress, prefix + "HomeAddress."));
                    //}

                    //if (HomeAddress == null && patient?.HomeAddress != null)
                    code.Emit(OpCodes.Ldarg_0);
                    code.Emit(OpCodes.Call, prop.GetMethod);
                    code.Emit(OpCodes.Brtrue_S, propIsNotNull);
                    code.Emit(OpCodes.Ldloc, target);
                    code.Emit(OpCodes.Brtrue_S, targetIsNotNull);
                    code.Emit(OpCodes.Ldnull);
                    code.Emit(OpCodes.Br_S, targetIsNull);
                    code.MarkLabel(targetIsNotNull);
                    code.Emit(OpCodes.Ldloc, target);
                    code.Emit(OpCodes.Callvirt, prop.GetMethod);
                    code.MarkLabel(targetIsNull);
                    code.Emit(OpCodes.Ldnull);
                    code.Emit(OpCodes.Cgt_Un);
                    code.Emit(OpCodes.Br_S, checkBothProps);
                    code.MarkLabel(propIsNotNull);
                    code.Emit(OpCodes.Ldc_I4_0);
                    code.MarkLabel(checkBothProps);
                    code.Emit(OpCodes.Brfalse_S, bothPropsExist);
                    //    changes.Add(new AuditedChange() { PropertyName = prefix + "HomeAddress", ChangeType = AuditChangeType.Removed, ItemIndex = 0, Value = NameFirst });
                    code.Emit(OpCodes.Ldloc, changes);
                    code.Emit(OpCodes.Newobj, typeof(AuditedChange).GetConstructor(Type.EmptyTypes));
                    code.Emit(OpCodes.Dup);
                    code.Emit(OpCodes.Ldarg_2); // prefix
                    code.Emit(OpCodes.Ldstr, prop.Name);
                    code.Emit(OpCodes.Call, typeof(string).GetMethod("Concat", new Type[] { typeof(string), typeof(string) }));
                    code.Emit(OpCodes.Callvirt, typeof(AuditedChange).GetProperty("PropertyName").SetMethod);
                    code.Emit(OpCodes.Dup);
                    code.Emit(OpCodes.Ldc_I4, (int)AuditChangeType.Removed);
                    code.Emit(OpCodes.Callvirt, typeof(AuditedChange).GetProperty("ChangeType").SetMethod);
                    code.Emit(OpCodes.Dup);
                    code.Emit(OpCodes.Ldc_I4, 0);
                    code.Emit(OpCodes.Callvirt, typeof(AuditedChange).GetProperty("ItemIndex").SetMethod);
                    code.Emit(OpCodes.Dup);
                    code.Emit(OpCodes.Ldarg_0);
                    code.Emit(OpCodes.Call, prop.GetMethod);
                    code.Emit(OpCodes.Callvirt, typeof(AuditedChange).GetProperty("Value").SetMethod);
                    code.Emit(OpCodes.Callvirt, changes.LocalType.GetMethod("Add"));
                    code.Emit(OpCodes.Br, jumpToEnd);

                    code.MarkLabel(bothPropsExist);
                    // else if (HomeAddress != null)
                    code.Emit(OpCodes.Ldarg_0);
                    code.Emit(OpCodes.Call, prop.GetMethod);
                    code.Emit(OpCodes.Ldnull);
                    code.Emit(OpCodes.Cgt_Un);
                    code.Emit(OpCodes.Brfalse, jumpToEnd);

                    //if (target?.HomeAddress == null)
                    //{
                    //    changes.Add(new AuditedChange() { PropertyName = prefix + "HomeAddress", ChangeType = AuditChangeType.NewOrModified, ItemIndex = 0, Value = HomeAddress });
                    //}
                    code.Emit(OpCodes.Ldloc, target);
                    code.Emit(OpCodes.Brtrue_S, targetIsNotNull3);
                    code.Emit(OpCodes.Ldnull);
                    code.Emit(OpCodes.Br_S, targetIsNull3);
                    code.MarkLabel(targetIsNotNull3);
                    code.Emit(OpCodes.Ldloc, target);
                    code.Emit(OpCodes.Callvirt, prop.GetMethod);
                    code.MarkLabel(targetIsNull3);
                    code.Emit(OpCodes.Ldnull);
                    code.Emit(OpCodes.Ceq);
                    code.Emit(OpCodes.Brfalse_S, targetIsNotNull4);
                    code.Emit(OpCodes.Ldloc, changes);
                    code.Emit(OpCodes.Newobj, typeof(AuditedChange).GetConstructor(Type.EmptyTypes));
                    code.Emit(OpCodes.Dup);
                    code.Emit(OpCodes.Ldarg_2); // prefix
                    code.Emit(OpCodes.Ldstr, prop.Name);
                    code.Emit(OpCodes.Call, typeof(string).GetMethod("Concat", new Type[] { typeof(string), typeof(string) }));
                    code.Emit(OpCodes.Callvirt, typeof(AuditedChange).GetProperty("PropertyName").SetMethod);
                    code.Emit(OpCodes.Dup);
                    code.Emit(OpCodes.Ldc_I4, (int)AuditChangeType.NewOrModified);
                    code.Emit(OpCodes.Callvirt, typeof(AuditedChange).GetProperty("ChangeType").SetMethod);
                    code.Emit(OpCodes.Dup);
                    code.Emit(OpCodes.Ldc_I4, 0);
                    code.Emit(OpCodes.Callvirt, typeof(AuditedChange).GetProperty("ItemIndex").SetMethod);
                    code.Emit(OpCodes.Dup);
                    code.Emit(OpCodes.Ldarg_0);
                    code.Emit(OpCodes.Call, prop.GetMethod);
                    code.Emit(OpCodes.Callvirt, typeof(AuditedChange).GetProperty("Value").SetMethod);
                    code.Emit(OpCodes.Callvirt, changes.LocalType.GetMethod("Add"));
                    code.Emit(OpCodes.Br, jumpToEnd);

                    // else
                    code.MarkLabel(targetIsNotNull4);
                    // changes.AddRange(HomeAddress.Compare(patient?.HomeAddress, prefix + "HomeAddress."));
                    code.Emit(OpCodes.Ldloc, changes);
                    code.Emit(OpCodes.Ldarg_0);
                    code.Emit(OpCodes.Call, prop.GetMethod);
                    code.Emit(OpCodes.Ldloc, target);
                    code.Emit(OpCodes.Brtrue_S, targetIsNotNull2);
                    code.Emit(OpCodes.Ldnull);
                    code.Emit(OpCodes.Br_S, targetIsNull2);
                    code.MarkLabel(targetIsNotNull2);
                    code.Emit(OpCodes.Ldloc, target);
                    code.Emit(OpCodes.Callvirt, prop.GetMethod);
                    code.MarkLabel(targetIsNull2);
                    code.Emit(OpCodes.Ldarg_2);
                    code.Emit(OpCodes.Ldstr, prop.Name + ".");
                    code.Emit(OpCodes.Call, typeof(string).GetMethod("Concat", new Type[] { typeof(string), typeof(string) }));
                    code.Emit(OpCodes.Callvirt, typeof(IModel).GetMethod("Compare"));
                    code.Emit(OpCodes.Callvirt, changes.LocalType.GetMethod("AddRange"));

                    code.MarkLabel(jumpToEnd);
                }
                else if (prop.PropertyType.Implements<IEnumerable>() 
                    && !prop.PropertyType.IsValueType 
                    && !prop.PropertyType.Equals(typeof(string))
                    && !prop.PropertyType.Equals(typeof(byte[]))
                    && !prop.PropertyType.Equals(typeof(char[]))
                    && !prop.PropertyType.Equals(typeof(int[]))
                    && !prop.PropertyType.Equals(typeof(long[]))
                    && !prop.PropertyType.Equals(typeof(short[]))
                    && !prop.PropertyType.Equals(typeof(uint[]))
                    && !prop.PropertyType.Equals(typeof(ulong[]))
                    && !prop.PropertyType.Equals(typeof(ushort[]))
                    && !prop.PropertyType.Equals(typeof(float[]))
                    && !prop.PropertyType.Equals(typeof(double[]))
                    && !prop.PropertyType.Equals(typeof(decimal[])))
                {
                    var targetIsNotNull = code.DefineLabel();
                    var targetIsNull = code.DefineLabel();

                    // changes.AddRange(ModelPropertyComparer.Compare(OtherAddresses, target?.OtherAddresses, prefix + "OtherAddresses"));
                    code.Emit(OpCodes.Ldloc, changes);
                    code.Emit(OpCodes.Ldarg_0);
                    code.Emit(OpCodes.Call, prop.GetMethod);
                    code.Emit(OpCodes.Ldloc, target);
                    code.Emit(OpCodes.Brtrue_S, targetIsNotNull);
                    code.Emit(OpCodes.Ldnull);
                    code.Emit(OpCodes.Br_S, targetIsNull);
                    code.MarkLabel(targetIsNotNull);
                    code.Emit(OpCodes.Ldloc, target);
                    code.Emit(OpCodes.Callvirt, prop.GetMethod);
                    code.MarkLabel(targetIsNull);
                    code.Emit(OpCodes.Ldarg_2);
                    code.Emit(OpCodes.Ldstr, prop.Name);
                    code.Emit(OpCodes.Call, typeof(string).GetMethod("Concat", new Type[] { typeof(string), typeof(string) }));
                    code.Emit(OpCodes.Call, typeof(ModelPropertyComparer).GetMethod("Compare", BindingFlags.Public | BindingFlags.Static));
                    code.Emit(OpCodes.Callvirt, changes.LocalType.GetMethod("AddRange"));
                }
                else
                {

                    var targetIsNotNull = code.DefineLabel();
                    var targetIsNull = code.DefineLabel();


                    // changes.AddRange(ModelPropertyComparer.CompareValues(ModelType, patient?.ModelType, prefix + "ModelType"));
                    code.Emit(OpCodes.Ldloc, changes);  // stack 0 - changes
                    code.Emit(OpCodes.Ldarg_0); // stack 1 - this
                    code.Emit(OpCodes.Call, prop.GetMethod); // stack 1 - property value
                    code.Emit(OpCodes.Ldloc, target); // stack 2 - target
                    code.Emit(OpCodes.Brtrue_S, targetIsNotNull); // stack 2 - popped
                    code.Emit(OpCodes.Ldnull); // stack 2 - null
                    code.Emit(OpCodes.Br_S, targetIsNull);
                    code.MarkLabel(targetIsNotNull);
                    code.Emit(OpCodes.Ldloc, target); // stack 2 - target
                    code.Emit(OpCodes.Callvirt, prop.GetMethod); // stack 2 - target property value
                    code.MarkLabel(targetIsNull);
                    code.Emit(OpCodes.Ldarg_2);  // stack 3 - prefix
                    code.Emit(OpCodes.Ldstr, prop.Name); // stack 4 - property name
                    code.Emit(OpCodes.Call, typeof(string).GetMethod("Concat", new Type[] { typeof(string), typeof(string) }));  // stack 3 - long property name
                    code.Emit(OpCodes.Call, typeof(ModelPropertyComparer).GetMethod("CompareValues", BindingFlags.Public | BindingFlags.Static).MakeGenericMethod(prop.PropertyType));
                    code.Emit(OpCodes.Callvirt, changes.LocalType.GetMethod("AddRange"));
                }
            }

            code.Emit(OpCodes.Ldloc, changes);
            code.Emit(OpCodes.Ret);

            typeBuilder.DefineMethodOverride(method, typeof(IModel).GetMethod("Compare"));

            return method;
        }

        private static PropertyInfo BuildProtocolBufferProperty(TypeBuilder typeBuilder, Type interfaceType, out FieldInfo protoBuffField)
        {
            var propType = typeof(byte[]);
            var piName = "ProtocolBuffer";
            protoBuffField = typeBuilder.DefineField("_" + piName.ToLower(), propType, FieldAttributes.Public);

            var property = typeBuilder.DefineProperty(piName,
                PropertyAttributes.HasDefault,
                propType,
                null);

            var getter = typeBuilder.DefineMethod("get_" + piName,
                MethodAttributes.Public
                | MethodAttributes.SpecialName
                | MethodAttributes.HideBySig
                | MethodAttributes.NewSlot
                | MethodAttributes.Final
                | MethodAttributes.Virtual,
                propType,
                Type.EmptyTypes);

            var getterCode = getter.GetILGenerator();
            getterCode.Emit(OpCodes.Ldarg_0);
            getterCode.Emit(OpCodes.Ldfld, protoBuffField);
            getterCode.Emit(OpCodes.Ret);
            property.SetGetMethod(getter);
            typeBuilder.DefineMethodOverride(getter, typeof(IBinarySerializable).GetProperty(piName).GetMethod);

            var setter = typeBuilder.DefineMethod("set_" + piName,
                MethodAttributes.Public
                | MethodAttributes.SpecialName
                | MethodAttributes.HideBySig
                | MethodAttributes.NewSlot
                | MethodAttributes.Final
                | MethodAttributes.Virtual,
                null,
                new[] { propType });

            var setterCode = setter.GetILGenerator();
            setterCode.Emit(OpCodes.Ldarg_0);
            setterCode.Emit(OpCodes.Ldarg_1);
            setterCode.Emit(OpCodes.Stfld, protoBuffField);
            setterCode.Emit(OpCodes.Ret);
            property.SetSetMethod(setter);
            typeBuilder.DefineMethodOverride(setter, typeof(IBinarySerializable).GetProperty(piName).SetMethod);

            return property;
        }

        private static MethodInfo BuildFromBytes(Type interfaceType, TypeBuilder typeBuilder, List<PropertyInfo> props, Type[] additionalInterfaces)
        {
            var method = typeBuilder.DefineMethod("FromBytes",
                MethodAttributes.Public
                | MethodAttributes.HideBySig
                | MethodAttributes.NewSlot
                | MethodAttributes.Virtual
                | MethodAttributes.Final,
                CallingConventions.Standard,
                typeof(void),
                new Type[] { typeof(byte[]) });
            var code = method.GetILGenerator();
            var exit = code.DefineLabel();
            var endfinally1 = code.DefineLabel();
            var endfinally2 = code.DefineLabel();

            var ms = code.DeclareLocal(typeof(MemoryStream));
            var bw = code.DeclareLocal(typeof(BinaryReader));

            code.Emit(OpCodes.Ldarg_1);
            code.Emit(OpCodes.Newobj, typeof(MemoryStream).GetConstructor(new Type[] { typeof(byte[]) }));
            code.Emit(OpCodes.Stloc_0);

            code.BeginExceptionBlock();
            code.Emit(OpCodes.Ldloc_0);
            code.Emit(OpCodes.Newobj, typeof(BinaryReader).GetConstructor(new Type[] { typeof(Stream) }));
            code.Emit(OpCodes.Stloc_1);
            code.BeginExceptionBlock();

            DeserializeMembers(typeBuilder, interfaceType, code, exit, bw, props, additionalInterfaces);

            code.BeginFinallyBlock();
            code.Emit(OpCodes.Ldloc_1);
            code.Emit(OpCodes.Brfalse_S, endfinally2);
            code.Emit(OpCodes.Ldloc_1);
            code.Emit(OpCodes.Callvirt, typeof(IDisposable).GetMethod("Dispose"));
            code.MarkLabel(endfinally2);
            code.EndExceptionBlock();

            code.BeginFinallyBlock();
            code.Emit(OpCodes.Ldloc_0);
            code.Emit(OpCodes.Brfalse, endfinally1);
            code.Emit(OpCodes.Ldloc_0);
            code.Emit(OpCodes.Callvirt, typeof(IDisposable).GetMethod("Dispose"));
            code.MarkLabel(endfinally1);
            code.EndExceptionBlock();

            code.MarkLabel(exit);
            code.Emit(OpCodes.Ret);

            typeBuilder.DefineMethodOverride(method, typeof(IBinarySerializable).GetMethod("FromBytes"));

            return method;
        }

        private static void DeserializeMembers(TypeBuilder typeBuilder, Type interfaceType, ILGenerator methodCode, Label exit, LocalBuilder br, List<PropertyInfo> props, Type[] additionalInterfaces)
        {
            var sorter = new PropertySorter(interfaceType, additionalInterfaces);
            var serializables = sorter.Filter(props);
            serializables.Sort(sorter);

            foreach (var member in serializables)
            {
                CheckStreamPosition(methodCode, exit, br);
                if (IsValueType(member))
                {
                    DeserializeValueType(typeBuilder, interfaceType, methodCode, member, br);
                }
                else if (IsNullableValueType(member))
                {
                    DeserializeNullableValueType(typeBuilder, interfaceType, methodCode, member, br);
                }
                else
                {
                    var memberType = member.PropertyType;
                    if (memberType == typeof(byte[]))
                    {
                        DeserializeByteArray(typeBuilder, interfaceType, methodCode, member, br);
                    }
                    else if (memberType == typeof(char[]))
                    {
                        DeserializeCharArray(typeBuilder, interfaceType, methodCode, member, br);
                    }
                    else if (memberType == typeof(DateTime))
                    {
                        DeserializeDateTime(typeBuilder, interfaceType, methodCode, member, br);
                    }
                    else if (memberType == typeof(string))
                    {
                        DeserializeString(typeBuilder, interfaceType, methodCode, member, br);
                    }
                    else if (memberType == typeof(DateTime?))
                    {
                        DeserializeNullableDateTime(typeBuilder, interfaceType, methodCode, member, br);
                    }
                    else if (memberType.IsArray && !memberType.GetElementType().Implements<IModel>())
                    {
                        DeserializeArray(typeBuilder, interfaceType, methodCode, member, br);
                    }
                    else if (memberType.Implements<IModel>())
                    {
                        DeserializeModel(typeBuilder, interfaceType, methodCode, member, br);
                    }
                    else if (memberType.Implements(typeof(IEnumerable<>)) 
                        && ((memberType.IsGenericType && memberType.GetGenericArguments()[0].Implements<IModel>()) || (memberType.IsArray && memberType.GetElementType().Implements<IModel>())))
                    {
                        DeserializeEnumerableModels(typeBuilder, interfaceType, methodCode, member, br);
                    }
                    else
                    {
                        DeserializeObject(typeBuilder, interfaceType, methodCode, member, br);
                    }
                }
            }
        }

        private static void DeserializeEnumerableModels(TypeBuilder typeBuilder, Type interfaceType, ILGenerator methodCode, PropertyInfo member, LocalBuilder br)
        {
            if (member.PropertyType.IsArray)
            {
                DeserializeArrayModels(typeBuilder, interfaceType, methodCode, member, br);
            }
            else if (member.PropertyType.Implements<ICollection>())
            {
                DeserializeListModels(typeBuilder, interfaceType, methodCode, member, br);
            }
            else
                throw new NotSupportedException("The enumerable IModel collection type is not supported.  The must either be an Array or ICollection.");
        }

        private static void DeserializeListModels(TypeBuilder typeBuilder, Type interfaceType, ILGenerator methodCode, PropertyInfo member, LocalBuilder br)
        {
            /*

            IL_022b:  ldloc.1
            IL_022c:  callvirt   instance bool [mscorlib]System.IO.BinaryReader::ReadBoolean()
            IL_0231:  stloc.s    moreChildrenIsNotNull
            IL_0233:  ldloc.s    moreChildrenIsNotNull
            IL_0235:  stloc.s    V_25
            IL_0237:  ldloc.s    V_25
            IL_0239:  brfalse.s  IL_02a6
            IL_023b:  nop
            IL_023c:  ldloc.1
            IL_023d:  callvirt   instance int32 [mscorlib]System.IO.BinaryReader::ReadInt32()
            IL_0242:  stloc.s    V_26
            IL_0244:  ldarg.0
            IL_0245:  newobj     instance void class [mscorlib]System.Collections.Generic.List`1<class Data.Core.Compilation.IChild>::.ctor()
            IL_024a:  call       instance void Data.Core.Compilation.EmittedModel::set_MoreChildren(class [mscorlib]System.Collections.Generic.List`1<class Data.Core.Compilation.IChild>)
            IL_024f:  nop
            IL_0250:  ldc.i4.0
            IL_0251:  stloc.s    V_27
            IL_0253:  br.s       IL_0299
            IL_0255:  nop
            IL_0256:  ldloc.1
            IL_0257:  callvirt   instance int32 [mscorlib]System.IO.BinaryReader::ReadInt32()
            IL_025c:  stloc.s    V_28
            IL_025e:  ldloc.1
            IL_025f:  ldloc.s    V_28
            IL_0261:  callvirt   instance uint8[] [mscorlib]System.IO.BinaryReader::ReadBytes(int32)
            IL_0266:  stloc.s    V_29
            IL_0268:  ldnull
            IL_0269:  ldc.i4.1
            IL_026a:  call       !!0 Data.Core.Compilation.RuntimeModelBuilder::CreateModelInstance<class Data.Core.Compilation.IChild>(class [mscorlib]System.Type,
                                                                                                                                                bool)
            IL_026f:  stloc.s    V_30
            IL_0271:  ldloc.s    V_30
            IL_0273:  castclass  [Common]Common.Serialization.Binary.IBinarySerializable
            IL_0278:  ldloc.s    V_29
            IL_027a:  callvirt   instance void [Common]Common.Serialization.Binary.IBinarySerializable::FromBytes(uint8[])
            IL_027f:  nop
            IL_0280:  ldarg.0
            IL_0281:  call       instance class [mscorlib]System.Collections.Generic.List`1<class Data.Core.Compilation.IChild> Data.Core.Compilation.EmittedModel::get_MoreChildren()
            IL_0286:  ldloc.s    V_30
            IL_0288:  callvirt   instance void class [mscorlib]System.Collections.Generic.List`1<class Data.Core.Compilation.IChild>::Add(!0)
            IL_028d:  nop
            IL_028e:  nop
            IL_028f:  ldloc.s    V_27
            IL_0291:  stloc.s    V_22
            IL_0293:  ldloc.s    V_22
            IL_0295:  ldc.i4.1
            IL_0296:  add
            IL_0297:  stloc.s    V_27
            IL_0299:  ldloc.s    V_27
            IL_029b:  ldloc.s    V_26
            IL_029d:  clt
            IL_029f:  stloc.s    V_31
            IL_02a1:  ldloc.s    V_31
            IL_02a3:  brtrue.s   IL_0255
            IL_02a5:  nop

            */


            var listIsNotNull = methodCode.DeclareLocal(typeof(bool));
            var count = methodCode.DeclareLocal(typeof(int));
            var length = methodCode.DeclareLocal(typeof(int));
            var bytes = methodCode.DeclareLocal(typeof(byte[]));
            var i = methodCode.DeclareLocal(typeof(int));
            var listIsNull = methodCode.DefineLabel();
            var item = methodCode.DeclareLocal(member.PropertyType.ResolveElementType());
            var loopStart = methodCode.DefineLabel();
            var loopCheck = methodCode.DefineLabel();

            methodCode.Emit(OpCodes.Ldarg_0);
            methodCode.Emit(OpCodes.Call, member.GetMethod);
            methodCode.Emit(OpCodes.Callvirt, member.PropertyType.GetMethod("Clear"));

            methodCode.Emit(OpCodes.Ldloc, br);
            methodCode.Emit(OpCodes.Callvirt, typeof(BinaryReader).GetMethod("ReadBoolean"));
            methodCode.Emit(OpCodes.Stloc, listIsNotNull);
            methodCode.Emit(OpCodes.Ldloc, listIsNotNull);
            methodCode.Emit(OpCodes.Brfalse_S, listIsNull);
            methodCode.Emit(OpCodes.Ldloc, br);
            methodCode.Emit(OpCodes.Callvirt, typeof(BinaryReader).GetMethod("ReadInt32"));
            methodCode.Emit(OpCodes.Stloc, count);
            //methodCode.Emit(OpCodes.Ldarg_0);
            //methodCode.Emit(OpCodes.Newobj, member.PropertyType.GetConstructor(Type.EmptyTypes));
            //methodCode.Emit(OpCodes.Call, member.SetMethod);
            methodCode.Emit(OpCodes.Ldc_I4_0);
            methodCode.Emit(OpCodes.Stloc, i);
            methodCode.Emit(OpCodes.Br_S, loopCheck);
            methodCode.MarkLabel(loopStart);
            methodCode.Emit(OpCodes.Ldloc, br);
            methodCode.Emit(OpCodes.Callvirt, typeof(BinaryReader).GetMethod("ReadInt32"));
            methodCode.Emit(OpCodes.Stloc, length);
            methodCode.Emit(OpCodes.Ldloc, br);
            methodCode.Emit(OpCodes.Ldloc, length);
            methodCode.Emit(OpCodes.Callvirt, typeof(BinaryReader).GetMethod("ReadBytes"));
            methodCode.Emit(OpCodes.Stloc, bytes);
            methodCode.Emit(OpCodes.Ldnull);
            methodCode.Emit(OpCodes.Ldc_I4_1);
            methodCode.Emit(OpCodes.Ldnull);
            var method = typeof(RuntimeModelBuilder).GetMethods(BindingFlags.Public | BindingFlags.Static)
                .Single(mi => mi.IsGenericMethodDefinition && mi.Name.Equals("CreateModelInstance"))
                .MakeGenericMethod(member.PropertyType.ResolveElementType());
            methodCode.Emit(OpCodes.Call, method);
            methodCode.Emit(OpCodes.Stloc, item);
            methodCode.Emit(OpCodes.Ldloc, item);
            methodCode.Emit(OpCodes.Castclass, typeof(IBinarySerializable));
            methodCode.Emit(OpCodes.Ldloc, bytes);
            methodCode.Emit(OpCodes.Callvirt, typeof(IBinarySerializable).GetMethod("FromBytes"));
            methodCode.Emit(OpCodes.Ldarg_0);
            methodCode.Emit(OpCodes.Call, member.GetMethod);
            methodCode.Emit(OpCodes.Ldloc, item);
            if (member.PropertyType.Implements<IList>())
            {
                methodCode.Emit(OpCodes.Callvirt, typeof(IList).GetMethod("Add"));
                methodCode.Emit(OpCodes.Pop);
            }
            else if (member.PropertyType.Implements(typeof(IList<>)))
            {
                var listType = typeof(ICollection<>).MakeGenericType(member.PropertyType.ResolveElementType());
                methodCode.Emit(OpCodes.Callvirt, listType.GetMethod("Add"));
            }
            else
                throw new InvalidOperationException("List property types must implement IList or IList<T> where T : IModel");

            methodCode.Emit(OpCodes.Ldloc, i);
            methodCode.Emit(OpCodes.Ldc_I4_1);
            methodCode.Emit(OpCodes.Add);
            methodCode.Emit(OpCodes.Stloc, i);

            methodCode.MarkLabel(loopCheck);
            methodCode.Emit(OpCodes.Ldloc, i);
            methodCode.Emit(OpCodes.Ldloc, count);
            methodCode.Emit(OpCodes.Clt);
            methodCode.Emit(OpCodes.Brtrue_S, loopStart);


            methodCode.MarkLabel(listIsNull);
        }

        private static void DeserializeArrayModels(TypeBuilder typeBuilder, Type interfaceType, ILGenerator methodCode, PropertyInfo member, LocalBuilder br)
        {
            /*
            IL_018d:  ldloc.1
            IL_018e:  callvirt   instance bool [mscorlib]System.IO.BinaryReader::ReadBoolean()
            IL_0193:  stloc.3
            IL_0194:  ldloc.3
            IL_0195:  stloc.s    V_16
            IL_0197:  ldloc.s    V_16
            IL_0199:  brfalse.s  IL_0205
            IL_019b:  nop
            IL_019c:  ldloc.1
            IL_019d:  callvirt   instance int32 [mscorlib]System.IO.BinaryReader::ReadInt32()
            IL_01a2:  stloc.s    count
            IL_01a4:  ldarg.0
            IL_01a5:  ldloc.s    count
            IL_01a7:  newarr     Data.Core.Compilation.IChild
            IL_01ac:  call       instance void Data.Core.Compilation.EmittedModel::set_Children(class Data.Core.Compilation.IChild[])
            IL_01b1:  nop
            IL_01b2:  ldc.i4.0
            IL_01b3:  stloc.s    i
            IL_01b5:  br.s       IL_01f8
            IL_01b7:  nop
            IL_01b8:  ldloc.1
            IL_01b9:  callvirt   instance int32 [mscorlib]System.IO.BinaryReader::ReadInt32()
            IL_01be:  stloc.s    V_19
            IL_01c0:  ldloc.1
            IL_01c1:  ldloc.s    V_19
            IL_01c3:  callvirt   instance uint8[] [mscorlib]System.IO.BinaryReader::ReadBytes(int32)
            IL_01c8:  stloc.s    V_20
            IL_01ca:  ldnull
            IL_01cb:  ldc.i4.1
            IL_01cc:  call       !!0 Data.Core.Compilation.RuntimeModelBuilder::CreateModelInstance<class Data.Core.Compilation.IChild>(class [mscorlib]System.Type,
                                                                                                                                                bool)
            IL_01d1:  stloc.s    child
            IL_01d3:  ldloc.s    child
            IL_01d5:  castclass  [Common]Common.Serialization.Binary.IBinarySerializable
            IL_01da:  ldloc.s    V_20
            IL_01dc:  callvirt   instance void [Common]Common.Serialization.Binary.IBinarySerializable::FromBytes(uint8[])
            IL_01e1:  nop
            IL_01e2:  ldarg.0
            IL_01e3:  call       instance class Data.Core.Compilation.IChild[] Data.Core.Compilation.EmittedModel::get_Children()
            IL_01e8:  ldloc.s    i
            IL_01ea:  ldloc.s    child
            IL_01ec:  stelem.ref
            IL_01ed:  nop
            IL_01ee:  ldloc.s    i
            IL_01f0:  stloc.s    V_22
            IL_01f2:  ldloc.s    V_22
            IL_01f4:  ldc.i4.1
            IL_01f5:  add
            IL_01f6:  stloc.s    i
            IL_01f8:  ldloc.s    i
            IL_01fa:  ldloc.s    count
            IL_01fc:  clt
            IL_01fe:  stloc.s    V_23
            IL_0200:  ldloc.s    V_23
            IL_0202:  brtrue.s   IL_01b7

            */

            var arrayIsNotNull = methodCode.DeclareLocal(typeof(bool));
            var count = methodCode.DeclareLocal(typeof(int));
            var length = methodCode.DeclareLocal(typeof(int));
            var bytes = methodCode.DeclareLocal(typeof(byte[]));
            var i = methodCode.DeclareLocal(typeof(int));
            var array = methodCode.DeclareLocal(member.PropertyType);
            var arrayIsNull = methodCode.DefineLabel();
            var elem = methodCode.DeclareLocal(member.PropertyType.GetElementType());
            var loopStart = methodCode.DefineLabel();
            var loopCheck = methodCode.DefineLabel();

            methodCode.Emit(OpCodes.Ldloc, br);
            methodCode.Emit(OpCodes.Callvirt, typeof(BinaryReader).GetMethod("ReadBoolean"));
            methodCode.Emit(OpCodes.Stloc, arrayIsNotNull);
            methodCode.Emit(OpCodes.Ldloc, arrayIsNotNull);
            methodCode.Emit(OpCodes.Brfalse_S, arrayIsNull);
            methodCode.Emit(OpCodes.Ldloc, br);
            methodCode.Emit(OpCodes.Callvirt, typeof(BinaryReader).GetMethod("ReadInt32"));
            methodCode.Emit(OpCodes.Stloc, count);
            methodCode.Emit(OpCodes.Ldarg_0);
            methodCode.Emit(OpCodes.Ldloc, count);
            methodCode.Emit(OpCodes.Newarr, member.PropertyType.GetElementType());
            methodCode.Emit(OpCodes.Call, member.SetMethod);
            methodCode.Emit(OpCodes.Ldc_I4_0);
            methodCode.Emit(OpCodes.Stloc, i);
            methodCode.Emit(OpCodes.Br_S, loopCheck);
            methodCode.MarkLabel(loopStart);
            methodCode.Emit(OpCodes.Ldloc, br);
            methodCode.Emit(OpCodes.Callvirt, typeof(BinaryReader).GetMethod("ReadInt32"));
            methodCode.Emit(OpCodes.Stloc, length);
            methodCode.Emit(OpCodes.Ldloc, br);
            methodCode.Emit(OpCodes.Ldloc, length);
            methodCode.Emit(OpCodes.Callvirt, typeof(BinaryReader).GetMethod("ReadBytes"));
            methodCode.Emit(OpCodes.Stloc, bytes);
            methodCode.Emit(OpCodes.Ldnull);
            methodCode.Emit(OpCodes.Ldc_I4_1);
            methodCode.Emit(OpCodes.Ldnull);
            var method = typeof(RuntimeModelBuilder).GetMethods(BindingFlags.Public | BindingFlags.Static)
                .Single(mi => mi.IsGenericMethodDefinition && mi.Name.Equals("CreateModelInstance"))
                .MakeGenericMethod(member.PropertyType.ResolveElementType());
            methodCode.Emit(OpCodes.Call, method);
            methodCode.Emit(OpCodes.Stloc, elem);
            methodCode.Emit(OpCodes.Ldloc, elem);
            methodCode.Emit(OpCodes.Castclass, typeof(IBinarySerializable));
            methodCode.Emit(OpCodes.Ldloc, bytes);
            methodCode.Emit(OpCodes.Callvirt, typeof(IBinarySerializable).GetMethod("FromBytes"));
            methodCode.Emit(OpCodes.Ldarg_0);
            methodCode.Emit(OpCodes.Call, member.GetMethod);
            methodCode.Emit(OpCodes.Ldloc, i);
            methodCode.Emit(OpCodes.Ldloc, elem);
            methodCode.Emit(OpCodes.Stelem_Ref);
            methodCode.Emit(OpCodes.Ldloc, i);
            methodCode.Emit(OpCodes.Ldc_I4_1);
            methodCode.Emit(OpCodes.Add);
            methodCode.Emit(OpCodes.Stloc, i);

            methodCode.MarkLabel(loopCheck);
            methodCode.Emit(OpCodes.Ldloc, i);
            methodCode.Emit(OpCodes.Ldloc, count);
            methodCode.Emit(OpCodes.Clt);
            methodCode.Emit(OpCodes.Brtrue_S, loopStart);

            methodCode.MarkLabel(arrayIsNull);
        }

        private static void DeserializeModel(TypeBuilder typeBuilder, Type interfaceType, ILGenerator methodCode, PropertyInfo member, LocalBuilder br)
        {
            /*

            IL_0124:  ldloc.1
            IL_0125:  callvirt   instance bool [mscorlib]System.IO.BinaryReader::ReadBoolean()
            IL_012a:  stloc.2
            IL_012b:  ldloc.2
            IL_012c:  stloc.s    V_12
            IL_012e:  ldloc.s    V_12
            IL_0130:  brfalse.s  IL_0167
            IL_0132:  nop
            IL_0133:  ldloc.1
            IL_0134:  callvirt   instance int32 [mscorlib]System.IO.BinaryReader::ReadInt32()
            IL_0139:  stloc.s    length
            IL_013b:  ldloc.1
            IL_013c:  ldloc.s    length
            IL_013e:  callvirt   instance uint8[] [mscorlib]System.IO.BinaryReader::ReadBytes(int32)
            IL_0143:  stloc.s    bytes
            IL_0145:  ldarg.0
            IL_0146:  ldnull
            IL_0147:  ldc.i4.1
            IL_0148:  call       !!0 Data.Core.Compilation.RuntimeModelBuilder::CreateModelInstance<class Data.Core.Compilation.IChild>(class [mscorlib]System.Type,
                                                                                                                                                bool)
            IL_014d:  call       instance void Data.Core.Compilation.EmittedModel::set_Child(class Data.Core.Compilation.IChild)
            IL_0152:  nop
            IL_0153:  ldarg.0
            IL_0154:  call       instance class Data.Core.Compilation.IChild Data.Core.Compilation.EmittedModel::get_Child()
            IL_0159:  castclass  [Common]Common.Serialization.Binary.IBinarySerializable
            IL_015e:  ldloc.s    bytes
            IL_0160:  callvirt   instance void [Common]Common.Serialization.Binary.IBinarySerializable::FromBytes(uint8[])


            */

            var modelIsNotNull = methodCode.DeclareLocal(typeof(bool));
            var length = methodCode.DeclareLocal(typeof(int));
            var bytes = methodCode.DeclareLocal(typeof(byte[]));
            var modelIsNull = methodCode.DefineLabel();

            methodCode.Emit(OpCodes.Ldloc, br);
            methodCode.Emit(OpCodes.Callvirt, typeof(BinaryReader).GetMethod("ReadBoolean"));
            methodCode.Emit(OpCodes.Stloc, modelIsNotNull);
            methodCode.Emit(OpCodes.Ldloc, modelIsNotNull);
            methodCode.Emit(OpCodes.Brfalse_S, modelIsNull);
            if (member.PropertyType == typeof(IModel) || member.PropertyType == typeof(ILink))
            {
                /* 
                IL_0041:  ldloc.1
                IL_0042:  callvirt   instance string [mscorlib]System.IO.BinaryReader::ReadString()
                IL_0047:  call       class [mscorlib]System.Type Data.Core.ModelTypeManager::GetModelType(string)
                IL_004c:  stloc.s    modelType
                IL_004e:  ldloc.1
                IL_004f:  callvirt   instance int32 [mscorlib]System.IO.BinaryReader::ReadInt32()
                IL_0054:  stloc.s    length
                IL_0056:  ldloc.1
                IL_0057:  ldloc.s    length
                IL_0059:  callvirt   instance uint8[] [mscorlib]System.IO.BinaryReader::ReadBytes(int32)
                IL_005e:  stloc.s    bytes
                IL_0060:  ldarg.0
                IL_0061:  ldloc.s    modelType
                IL_0063:  ldnull
                IL_0064:  ldc.i4.1
                IL_0065:  call       object Data.Core.Compilation.RuntimeModelBuilder::CreateModelInstance(class [mscorlib]System.Type,
                                                                                                                class [mscorlib]System.Type,
                                                                                                                bool)
                IL_006a:  castclass  Data.Core.Compilation.IChild
                IL_006f:  call       instance void Data.Core.Compilation.EmittedModel::set_Child(class Data.Core.Compilation.IChild)
                IL_0074:  nop
                IL_0075:  ldarg.0
                IL_0076:  call       instance class Data.Core.Compilation.IChild Data.Core.Compilation.EmittedModel::get_Child()
                IL_007b:  castclass  [Common]Common.Serialization.Binary.IBinarySerializable
                IL_0080:  ldloc.s    bytes
                IL_0082:  callvirt   instance void [Common]Common.Serialization.Binary.IBinarySerializable::FromBytes(uint8[])
                */
                var modelType = methodCode.DeclareLocal(typeof(Type));
                methodCode.Emit(OpCodes.Ldloc, br);
                methodCode.Emit(OpCodes.Callvirt, typeof(BinaryReader).GetMethod("ReadString"));
                methodCode.Emit(OpCodes.Call, typeof(ModelTypeManager).GetMethods(BindingFlags.Public | BindingFlags.Static)
                                                                      .Single(m => m.Name.Equals("GetModelType") && m.GetParameters()[0].ParameterType.Equals(typeof(string))));
                methodCode.Emit(OpCodes.Stloc, modelType);
                methodCode.Emit(OpCodes.Ldloc, br);
                methodCode.Emit(OpCodes.Callvirt, typeof(BinaryReader).GetMethod("ReadInt32"));
                methodCode.Emit(OpCodes.Stloc, length);
                methodCode.Emit(OpCodes.Ldloc, br);
                methodCode.Emit(OpCodes.Ldloc, length);
                methodCode.Emit(OpCodes.Callvirt, typeof(BinaryReader).GetMethod("ReadBytes"));
                methodCode.Emit(OpCodes.Stloc, bytes);
                methodCode.Emit(OpCodes.Ldarg_0);
                methodCode.Emit(OpCodes.Ldloc, modelType);
                methodCode.Emit(OpCodes.Ldnull);
                methodCode.Emit(OpCodes.Ldc_I4_1);
                methodCode.Emit(OpCodes.Ldnull);
                var method = typeof(RuntimeModelBuilder).GetMethods(BindingFlags.Public | BindingFlags.Static)
                    .Single(mi => !mi.IsGenericMethodDefinition && mi.Name.Equals("CreateModelInstance"));
                methodCode.Emit(OpCodes.Call, method);
                methodCode.Emit(OpCodes.Castclass, member.PropertyType);
                methodCode.Emit(OpCodes.Call, member.SetMethod);
                methodCode.Emit(OpCodes.Ldarg_0);
                methodCode.Emit(OpCodes.Call, member.GetMethod);
                methodCode.Emit(OpCodes.Castclass, typeof(IBinarySerializable));
                methodCode.Emit(OpCodes.Ldloc, bytes);
                methodCode.Emit(OpCodes.Callvirt, typeof(IBinarySerializable).GetMethod("FromBytes"));
            }
            else
            {
                methodCode.Emit(OpCodes.Ldloc, br);
                methodCode.Emit(OpCodes.Callvirt, typeof(BinaryReader).GetMethod("ReadInt32"));
                methodCode.Emit(OpCodes.Stloc, length);
                methodCode.Emit(OpCodes.Ldloc, br);
                methodCode.Emit(OpCodes.Ldloc, length);
                methodCode.Emit(OpCodes.Callvirt, typeof(BinaryReader).GetMethod("ReadBytes"));
                methodCode.Emit(OpCodes.Stloc, bytes);
                methodCode.Emit(OpCodes.Ldarg_0);
                methodCode.Emit(OpCodes.Ldnull);
                methodCode.Emit(OpCodes.Ldc_I4_1);
                methodCode.Emit(OpCodes.Ldnull);
                var method = typeof(RuntimeModelBuilder).GetMethods(BindingFlags.Public | BindingFlags.Static)
                    .Single(mi => mi.IsGenericMethodDefinition && mi.Name.Equals("CreateModelInstance"))
                    .MakeGenericMethod(member.PropertyType);
                methodCode.Emit(OpCodes.Call, method);
                methodCode.Emit(OpCodes.Call, member.SetMethod);
                methodCode.Emit(OpCodes.Ldarg_0);
                methodCode.Emit(OpCodes.Call, member.GetMethod);
                methodCode.Emit(OpCodes.Castclass, typeof(IBinarySerializable));
                methodCode.Emit(OpCodes.Ldloc, bytes);
                methodCode.Emit(OpCodes.Callvirt, typeof(IBinarySerializable).GetMethod("FromBytes"));
            }
            methodCode.MarkLabel(modelIsNull);
        }

        private static void DeserializeObject(TypeBuilder typeBuilder, Type interfaceType, ILGenerator methodCode, PropertyInfo member, LocalBuilder br)
        {
            var type = member.PropertyType;
            var elemType = type.GetElementType();
            var nullValue = methodCode.DefineLabel();

            // check if array is null
            methodCode.Emit(OpCodes.Ldloc, br);
            methodCode.Emit(OpCodes.Callvirt, typeof(BinaryReader).GetMethod("ReadBoolean"));
            methodCode.Emit(OpCodes.Brfalse, nullValue);


            methodCode.Emit(OpCodes.Ldarg_0); // object to write
            methodCode.Emit(OpCodes.Ldtoken, type);
            methodCode.Emit(OpCodes.Call, typeof(Type).GetMethod("GetTypeFromHandle", BindingFlags.Public | BindingFlags.Static));
            methodCode.Emit(OpCodes.Ldloc, br);
            methodCode.Emit(OpCodes.Call, typeof(Serialization.Binary._BinarySerializer).GetMethod("Deserialize", BindingFlags.Public | BindingFlags.Static));
            methodCode.Emit(OpCodes.Castclass, type);

            methodCode.Emit(OpCodes.Callvirt, ((PropertyInfo)member).SetMethod);


            methodCode.MarkLabel(nullValue);
        }

        private static void DeserializeArray(TypeBuilder typeBuilder, Type interfaceType, ILGenerator methodCode, PropertyInfo member, LocalBuilder br)
        {
            /*

            IL_003b:  ldloc.2
            IL_003c:  callvirt   instance bool [mscorlib]System.IO.BinaryReader::ReadBoolean()
            IL_0041:  stloc.s    V_5
            IL_0043:  ldloc.s    V_5
            IL_0045:  brfalse.s  IL_008b
            IL_0047:  nop
            IL_0048:  ldloc.2
            IL_0049:  callvirt   instance int32 [mscorlib]System.IO.BinaryReader::ReadInt32()
            IL_004e:  stloc.s    count
            IL_0050:  ldloc.s    count
            IL_0052:  newarr     [mscorlib]System.Int32
            IL_0057:  stloc.s    a
            IL_0059:  ldc.i4.0
            IL_005a:  stloc.s    i
            IL_005c:  br.s       IL_0075
            IL_005e:  nop
            IL_005f:  ldloc.s    a
            IL_0061:  ldloc.s    i
            IL_0063:  ldloc.2
            IL_0064:  callvirt   instance int32 [mscorlib]System.IO.BinaryReader::ReadInt32()
            IL_0069:  stelem.i4
            IL_006a:  nop
            IL_006b:  ldloc.s    i
            IL_006d:  stloc.s    V_9
            IL_006f:  ldloc.s    V_9
            IL_0071:  ldc.i4.1
            IL_0072:  add
            IL_0073:  stloc.s    i
            IL_0075:  ldloc.s    i
            IL_0077:  ldloc.s    count
            IL_0079:  clt
            IL_007b:  stloc.s    V_10
            IL_007d:  ldloc.s    V_10
            IL_007f:  brtrue.s   IL_005e
            IL_0081:  ldloc.0
            IL_0082:  ldloc.s    a
            IL_0084:  callvirt   instance void class 'Altus.Suffūz.Tests'.Array`1<int32>::set_A(!0[])
            IL_0089:  nop
            IL_008a:  nop
            IL_008b:  nop

            */
            var type = member.PropertyType;
            var elemType = type.GetElementType();
            var isNull = methodCode.DeclareLocal(typeof(bool));
            var count = methodCode.DeclareLocal(typeof(int));
            var array = methodCode.DeclareLocal(type);
            var i = methodCode.DeclareLocal(typeof(int));

            var nullValue = methodCode.DefineLabel();
            var countCheck = methodCode.DefineLabel();
            var loopStart = methodCode.DefineLabel();

            // check if array is null
            methodCode.Emit(OpCodes.Ldloc, br);
            methodCode.Emit(OpCodes.Callvirt, typeof(BinaryReader).GetMethod("ReadBoolean"));
            methodCode.Emit(OpCodes.Stloc, isNull);
            methodCode.Emit(OpCodes.Ldloc, isNull);
            methodCode.Emit(OpCodes.Brfalse, nullValue);

            // loop and set values
            methodCode.Emit(OpCodes.Ldloc, br);
            methodCode.Emit(OpCodes.Callvirt, typeof(BinaryReader).GetMethod("ReadInt32"));
            methodCode.Emit(OpCodes.Stloc, count);
            methodCode.Emit(OpCodes.Ldloc, count);
            methodCode.Emit(OpCodes.Newarr, elemType);
            methodCode.Emit(OpCodes.Stloc, array);
            methodCode.Emit(OpCodes.Ldc_I4_0);
            methodCode.Emit(OpCodes.Stloc, i);
            methodCode.Emit(OpCodes.Br, countCheck);
            methodCode.MarkLabel(loopStart);
            methodCode.Emit(OpCodes.Nop);

            if (IsValueType(elemType))
            {
                DeserializeValueTypeArrayElement(methodCode, array, i, elemType, br);
            }
            else if (IsNullableValueType(elemType))
            {
                DeserializeNullableValueTypeArrayElement(methodCode, array, i, elemType, br);
            }
            else if (elemType == typeof(string))
            {
                DeserializeStringArrayElement(methodCode, array, i, elemType, br);
            }
            else if (elemType == typeof(DateTime))
            {
                DeserializeDateTimeArrayElement(methodCode, array, i, elemType, br);
            }
            else if (elemType == typeof(DateTime?))
            {
                DeserializeNullableDateTimeArrayElement(methodCode, array, i, elemType, br);
            }
            else
                throw new NotSupportedException();

            // check iteration count
            methodCode.Emit(OpCodes.Ldloc, i);
            methodCode.Emit(OpCodes.Ldc_I4_1);
            methodCode.Emit(OpCodes.Add);
            methodCode.Emit(OpCodes.Stloc, i);
            methodCode.MarkLabel(countCheck);
            methodCode.Emit(OpCodes.Ldloc, i);
            methodCode.Emit(OpCodes.Ldloc, count);
            methodCode.Emit(OpCodes.Clt);
            methodCode.Emit(OpCodes.Brtrue, loopStart);
            // end loop

            methodCode.Emit(OpCodes.Ldarg_0);
            methodCode.Emit(OpCodes.Ldloc, array);
            methodCode.Emit(OpCodes.Callvirt, ((PropertyInfo)member).SetMethod);


            methodCode.MarkLabel(nullValue);
        }

        private static void DeserializeNullableDateTimeArrayElement(ILGenerator methodCode, LocalBuilder array, LocalBuilder i, Type elemType, LocalBuilder br)
        {
            var binaryDate = methodCode.DeclareLocal(typeof(long));
            var isNull = methodCode.DeclareLocal(typeof(bool));
            var readValue = methodCode.DeclareLocal(typeof(bool));
            var dontRead = methodCode.DefineLabel();

            methodCode.Emit(OpCodes.Ldloc, br); // binary reader
            methodCode.Emit(OpCodes.Callvirt, typeof(BinaryReader).GetMethod("ReadBoolean"));
            methodCode.Emit(OpCodes.Stloc, isNull);
            methodCode.Emit(OpCodes.Ldloc, isNull);
            methodCode.Emit(OpCodes.Ldc_I4_1);
            methodCode.Emit(OpCodes.Ceq);
            methodCode.Emit(OpCodes.Stloc, readValue);
            methodCode.Emit(OpCodes.Ldloc, readValue);
            methodCode.Emit(OpCodes.Brfalse_S, dontRead);

            methodCode.Emit(OpCodes.Ldloc, array);
            methodCode.Emit(OpCodes.Ldloc, i);
            methodCode.Emit(OpCodes.Ldloc, br); // binary reader
            methodCode.Emit(OpCodes.Callvirt, typeof(BinaryReader).GetMethod("ReadInt64"));
            methodCode.Emit(OpCodes.Stloc, binaryDate);
            methodCode.Emit(OpCodes.Ldloc, binaryDate); // object to write
            methodCode.Emit(OpCodes.Call, typeof(DateTime).GetMethod("FromBinary", BindingFlags.Public | BindingFlags.Static));
            methodCode.Emit(OpCodes.Newobj, typeof(DateTime?).GetConstructor(new Type[] { typeof(DateTime) }));
            methodCode.Emit(OpCodes.Stelem, elemType);

            methodCode.MarkLabel(dontRead);
        }

        private static void DeserializeDateTimeArrayElement(ILGenerator methodCode, LocalBuilder array, LocalBuilder i, Type elemType, LocalBuilder br)
        {
            methodCode.Emit(OpCodes.Ldloc, array);
            methodCode.Emit(OpCodes.Ldloc, i);
            methodCode.Emit(OpCodes.Ldloc, br); // binary reader
            methodCode.Emit(OpCodes.Callvirt, typeof(BinaryReader).GetMethod("ReadInt64"));
            methodCode.Emit(OpCodes.Call, typeof(DateTime).GetMethod("FromBinary", BindingFlags.Public | BindingFlags.Static));
            methodCode.Emit(OpCodes.Stelem, elemType);
        }

        private static void DeserializeStringArrayElement(ILGenerator methodCode, LocalBuilder array, LocalBuilder i, Type elemType, LocalBuilder br)
        {
            /*

            IL_005f:  ldloc.2
            IL_0060:  callvirt   instance bool [mscorlib]System.IO.BinaryReader::ReadBoolean()
            IL_0065:  stloc.s    V_9
            IL_0067:  ldloc.s    V_9
            IL_0069:  brfalse.s  IL_0078
            IL_006b:  nop
            IL_006c:  ldloc.s    a
            IL_006e:  ldloc.s    i
            IL_0070:  ldloc.2
            IL_0071:  callvirt   instance string [mscorlib]System.IO.BinaryReader::ReadString()
            IL_0076:  stelem.ref


            */

            var nullElement = methodCode.DefineLabel();
            methodCode.Emit(OpCodes.Ldloc, br);
            methodCode.Emit(OpCodes.Callvirt, typeof(BinaryReader).GetMethod("ReadBoolean"));
            methodCode.Emit(OpCodes.Brtrue, nullElement);
            methodCode.Emit(OpCodes.Ldloc, array);
            methodCode.Emit(OpCodes.Ldloc, i);
            methodCode.Emit(OpCodes.Ldloc, br);
            methodCode.Emit(OpCodes.Callvirt, typeof(BinaryReader).GetMethod("ReadString"));
            methodCode.Emit(OpCodes.Stelem_Ref);

            methodCode.MarkLabel(nullElement);
        }

        private static void DeserializeNullableValueTypeArrayElement(ILGenerator methodCode, LocalBuilder array, LocalBuilder i, Type elemType, LocalBuilder br)
        {
            var isNull = methodCode.DeclareLocal(typeof(bool));
            var readValue = methodCode.DeclareLocal(typeof(bool));
            var dontRead = methodCode.DefineLabel();
            var valueType = elemType.GetGenericArguments()[0];
            var baseType = valueType;

            if (valueType.IsEnum)
            {
                baseType = valueType.GetFields()[0].FieldType;
            }

            methodCode.Emit(OpCodes.Ldloc, br); // binary reader
            methodCode.Emit(OpCodes.Callvirt, typeof(BinaryReader).GetMethod("ReadBoolean"));
            methodCode.Emit(OpCodes.Stloc, isNull);
            methodCode.Emit(OpCodes.Ldloc, isNull);
            methodCode.Emit(OpCodes.Ldc_I4_1);
            methodCode.Emit(OpCodes.Ceq);
            methodCode.Emit(OpCodes.Stloc, readValue);
            methodCode.Emit(OpCodes.Ldloc, readValue);
            methodCode.Emit(OpCodes.Brfalse_S, dontRead);

            methodCode.Emit(OpCodes.Ldloc, array);
            methodCode.Emit(OpCodes.Ldloc, i);
            methodCode.Emit(OpCodes.Ldloc, br); // binary reader
            if (elemType == typeof(bool?))
            {
                methodCode.Emit(OpCodes.Callvirt, typeof(BinaryReader).GetMethod("ReadBoolean"));
            }
            else if (elemType == typeof(byte?) || baseType == typeof(byte))
            {
                methodCode.Emit(OpCodes.Callvirt, typeof(BinaryReader).GetMethod("ReadByte"));
            }
            else if (elemType == typeof(sbyte?) || baseType == typeof(sbyte))
            {
                methodCode.Emit(OpCodes.Callvirt, typeof(BinaryReader).GetMethod("ReadSByte"));
            }
            else if (elemType == typeof(char?) || baseType == typeof(char))
            {
                methodCode.Emit(OpCodes.Callvirt, typeof(BinaryReader).GetMethod("ReadChar"));
            }
            else if (elemType == typeof(short?) || baseType == typeof(short))
            {
                methodCode.Emit(OpCodes.Callvirt, typeof(BinaryReader).GetMethod("ReadInt16"));
            }
            else if (elemType == typeof(ushort?) || baseType == typeof(ushort))
            {
                methodCode.Emit(OpCodes.Callvirt, typeof(BinaryReader).GetMethod("ReadUInt16"));
            }
            else if (elemType == typeof(int?) || baseType == typeof(int))
            {
                methodCode.Emit(OpCodes.Callvirt, typeof(BinaryReader).GetMethod("ReadInt32"));
            }
            else if (elemType == typeof(uint?) || baseType == typeof(uint))
            {
                methodCode.Emit(OpCodes.Callvirt, typeof(BinaryReader).GetMethod("ReadUInt32"));
            }
            else if (elemType == typeof(long?) || baseType == typeof(long))
            {
                methodCode.Emit(OpCodes.Callvirt, typeof(BinaryReader).GetMethod("ReadInt64"));
            }
            else if (elemType == typeof(ulong?) || baseType == typeof(ulong))
            {
                methodCode.Emit(OpCodes.Callvirt, typeof(BinaryReader).GetMethod("ReadUInt64"));
            }
            else if (elemType == typeof(float?))
            {
                methodCode.Emit(OpCodes.Callvirt, typeof(BinaryReader).GetMethod("ReadSingle"));
            }
            else if (elemType == typeof(double?))
            {
                methodCode.Emit(OpCodes.Callvirt, typeof(BinaryReader).GetMethod("ReadDouble"));
            }
            else if (elemType == typeof(decimal?))
            {
                methodCode.Emit(OpCodes.Callvirt, typeof(BinaryReader).GetMethod("ReadDecimal"));
            }

            methodCode.Emit(OpCodes.Newobj, elemType.GetConstructor(new Type[] { valueType }));
            methodCode.Emit(OpCodes.Stelem, elemType);

            methodCode.MarkLabel(dontRead);
        }

        private static void DeserializeValueTypeArrayElement(ILGenerator methodCode, LocalBuilder array, LocalBuilder i, Type type, LocalBuilder br)
        {
            var baseType = type;

            if (type.IsEnum)
            {
                baseType = type.GetFields()[0].FieldType;
            }

            methodCode.Emit(OpCodes.Ldloc, array);
            methodCode.Emit(OpCodes.Ldloc, i);
            methodCode.Emit(OpCodes.Ldloc, br);

            if (type == typeof(bool))
            {
                methodCode.Emit(OpCodes.Callvirt, typeof(BinaryReader).GetMethod("ReadBoolean"));
            }
            else if (type == typeof(byte) || baseType == typeof(byte))
            {
                methodCode.Emit(OpCodes.Callvirt, typeof(BinaryReader).GetMethod("ReadByte"));
            }
            else if (type == typeof(sbyte) || baseType == typeof(sbyte))
            {
                methodCode.Emit(OpCodes.Callvirt, typeof(BinaryReader).GetMethod("ReadSByte"));
            }
            else if (type == typeof(char) || baseType == typeof(char))
            {
                methodCode.Emit(OpCodes.Callvirt, typeof(BinaryReader).GetMethod("ReadChar"));
            }
            else if (type == typeof(short) || baseType == typeof(short))
            {
                methodCode.Emit(OpCodes.Callvirt, typeof(BinaryReader).GetMethod("ReadInt16"));
            }
            else if (type == typeof(ushort) || baseType == typeof(ushort))
            {
                methodCode.Emit(OpCodes.Callvirt, typeof(BinaryReader).GetMethod("ReadUInt16"));
            }
            else if (type == typeof(int) || baseType == typeof(int))
            {
                methodCode.Emit(OpCodes.Callvirt, typeof(BinaryReader).GetMethod("ReadInt32"));
            }
            else if (type == typeof(uint) || baseType == typeof(uint))
            {
                methodCode.Emit(OpCodes.Callvirt, typeof(BinaryReader).GetMethod("ReadUInt32"));
            }
            else if (type == typeof(long) || baseType == typeof(long))
            {
                methodCode.Emit(OpCodes.Callvirt, typeof(BinaryReader).GetMethod("ReadInt64"));
            }
            else if (type == typeof(ulong) || baseType == typeof(ulong))
            {
                methodCode.Emit(OpCodes.Callvirt, typeof(BinaryReader).GetMethod("ReadUInt64"));
            }
            else if (type == typeof(float))
            {
                methodCode.Emit(OpCodes.Callvirt, typeof(BinaryReader).GetMethod("ReadSingle"));
            }
            else if (type == typeof(double))
            {
                methodCode.Emit(OpCodes.Callvirt, typeof(BinaryReader).GetMethod("ReadDouble"));
            }
            else if (type == typeof(decimal))
            {
                methodCode.Emit(OpCodes.Callvirt, typeof(BinaryReader).GetMethod("ReadDecimal"));
            }

            methodCode.Emit(OpCodes.Stelem, type);
        }

        private static void DeserializeNullableDateTime(TypeBuilder typeBuilder, Type interfaceType, ILGenerator methodCode, PropertyInfo member, LocalBuilder br)
        {
            var type = member.PropertyType;
            var binaryDate = methodCode.DeclareLocal(typeof(long));
            var date = methodCode.DeclareLocal(typeof(DateTime));
            var isNull = methodCode.DeclareLocal(typeof(bool));
            var readValue = methodCode.DeclareLocal(typeof(bool));
            var dontRead = methodCode.DefineLabel();

            methodCode.Emit(OpCodes.Ldloc, br); // binary reader
            methodCode.Emit(OpCodes.Callvirt, typeof(BinaryReader).GetMethod("ReadBoolean"));
            methodCode.Emit(OpCodes.Stloc, isNull);
            methodCode.Emit(OpCodes.Ldloc, isNull);
            methodCode.Emit(OpCodes.Ldc_I4_1);
            methodCode.Emit(OpCodes.Ceq);
            methodCode.Emit(OpCodes.Stloc, readValue);
            methodCode.Emit(OpCodes.Ldloc, readValue);
            methodCode.Emit(OpCodes.Brfalse_S, dontRead);

            methodCode.Emit(OpCodes.Ldloc, br); // binary reader
            methodCode.Emit(OpCodes.Callvirt, typeof(BinaryReader).GetMethod("ReadInt64"));
            methodCode.Emit(OpCodes.Stloc, binaryDate);
            methodCode.Emit(OpCodes.Ldloc, binaryDate); // object to write
            methodCode.Emit(OpCodes.Call, typeof(DateTime).GetMethod("FromBinary", BindingFlags.Public | BindingFlags.Static));
            methodCode.Emit(OpCodes.Stloc, date);
            methodCode.Emit(OpCodes.Ldarg_0); // object to write
            methodCode.Emit(OpCodes.Ldloc, date);
            methodCode.Emit(OpCodes.Newobj, typeof(DateTime?).GetConstructor(new Type[] { typeof(DateTime) }));
            
            methodCode.Emit(OpCodes.Callvirt, ((PropertyInfo)member).SetMethod);


            methodCode.MarkLabel(dontRead);
        }

        private static void DeserializeString(TypeBuilder typeBuilder, Type interfaceType, ILGenerator methodCode, PropertyInfo member, LocalBuilder br)
        {
            /*

            IL_003e:  callvirt   instance bool [mscorlib]System.IO.BinaryReader::ReadBoolean()
            IL_0043:  stloc.3
            IL_0044:  ldloc.3
            IL_0045:  ldc.i4.0
            IL_0046:  ceq
            IL_0048:  stloc.s    V_6
            IL_004a:  ldloc.s    V_6
            IL_004c:  brfalse.s  IL_006e
            IL_004e:  nop
            IL_004f:  ldloc.0
            IL_0050:  ldloc.2
            IL_0051:  callvirt   instance string [mscorlib]System.IO.BinaryReader::ReadString()
            IL_0056:  callvirt   instance void 'Altus.Suffūz.Tests'.SimplePOCO::set_Q(string)
            IL_006e:  nop

            */

            var text = methodCode.DeclareLocal(typeof(string));
            var isNull = methodCode.DeclareLocal(typeof(bool));
            var readValue = methodCode.DeclareLocal(typeof(bool));
            var dontRead = methodCode.DefineLabel();

            methodCode.Emit(OpCodes.Ldloc, br); // binary reader
            methodCode.Emit(OpCodes.Callvirt, typeof(BinaryReader).GetMethod("ReadBoolean"));
            methodCode.Emit(OpCodes.Stloc, isNull);
            methodCode.Emit(OpCodes.Ldloc, isNull);
            methodCode.Emit(OpCodes.Ldc_I4_0);
            methodCode.Emit(OpCodes.Ceq);
            methodCode.Emit(OpCodes.Stloc, readValue);
            methodCode.Emit(OpCodes.Ldloc, readValue);
            methodCode.Emit(OpCodes.Brfalse_S, dontRead);
            methodCode.Emit(OpCodes.Ldloc, br); // binary reader
            methodCode.Emit(OpCodes.Callvirt, typeof(BinaryReader).GetMethod("ReadString"));
            methodCode.Emit(OpCodes.Stloc, text);
            methodCode.Emit(OpCodes.Ldarg_0); // object to write
            methodCode.Emit(OpCodes.Ldloc, text);
            
            methodCode.Emit(OpCodes.Callvirt, ((PropertyInfo)member).SetMethod);
            methodCode.MarkLabel(dontRead);
        }

        private static void DeserializeDateTime(TypeBuilder typeBuilder, Type interfaceType, ILGenerator methodCode, PropertyInfo member, LocalBuilder br)
        {
            /*

            IL_003d:  ldloc.2
            IL_003e:  callvirt   instance int64 [mscorlib]System.IO.BinaryReader::ReadInt64()
            IL_0043:  stloc.3
            IL_0044:  ldloc.3
            IL_0045:  call       valuetype [mscorlib]System.DateTime [mscorlib]System.DateTime::FromBinary(int64)
            IL_004a:  stloc.s    'date'
            IL_004c:  ldloc.0
            IL_004d:  ldloc.s    'date'
            IL_004f:  callvirt   instance void 'Altus.Suffūz.Test'.SimplePOCO::set_P(valuetype [mscorlib]System.DateTime)

            */

            var binaryDate = methodCode.DeclareLocal(typeof(long));
            var date = methodCode.DeclareLocal(typeof(DateTime));

            methodCode.Emit(OpCodes.Ldloc, br); // binary reader
            methodCode.Emit(OpCodes.Callvirt, typeof(BinaryReader).GetMethod("ReadInt64"));
            methodCode.Emit(OpCodes.Stloc, binaryDate);
            methodCode.Emit(OpCodes.Ldloc, binaryDate); // object to write
            methodCode.Emit(OpCodes.Call, typeof(DateTime).GetMethod("FromBinary", BindingFlags.Public | BindingFlags.Static));
            methodCode.Emit(OpCodes.Stloc, date);
            methodCode.Emit(OpCodes.Ldarg_0); // object to write
            methodCode.Emit(OpCodes.Ldloc, date);
            
            methodCode.Emit(OpCodes.Callvirt, ((PropertyInfo)member).SetMethod);

        }

        private static void DeserializeCharArray(TypeBuilder typeBuilder, Type interfaceType, ILGenerator methodCode, PropertyInfo member, LocalBuilder br)
        {
            var array = methodCode.DeclareLocal(typeof(char[]));
            var arrayLength = methodCode.DeclareLocal(typeof(int));
            var isNull = methodCode.DeclareLocal(typeof(bool));
            var readValue = methodCode.DeclareLocal(typeof(bool));
            var dontRead = methodCode.DefineLabel();

            methodCode.Emit(OpCodes.Ldloc, br); // binary reader
            methodCode.Emit(OpCodes.Callvirt, typeof(BinaryReader).GetMethod("ReadBoolean"));
            methodCode.Emit(OpCodes.Stloc, isNull);
            methodCode.Emit(OpCodes.Ldloc, isNull);
            methodCode.Emit(OpCodes.Ldc_I4_0);
            methodCode.Emit(OpCodes.Ceq);
            methodCode.Emit(OpCodes.Stloc, readValue);
            methodCode.Emit(OpCodes.Ldloc, readValue);
            methodCode.Emit(OpCodes.Brfalse_S, dontRead);
            methodCode.Emit(OpCodes.Ldloc, br);
            methodCode.Emit(OpCodes.Callvirt, typeof(BinaryReader).GetMethod("ReadInt32"));
            methodCode.Emit(OpCodes.Stloc, arrayLength);

            methodCode.Emit(OpCodes.Ldloc, br);
            methodCode.Emit(OpCodes.Ldloc, arrayLength);
            methodCode.Emit(OpCodes.Callvirt, typeof(BinaryReader).GetMethod("ReadChars"));
            methodCode.Emit(OpCodes.Stloc, array);

            methodCode.Emit(OpCodes.Ldarg_0);
            methodCode.Emit(OpCodes.Ldloc, array);
            
            methodCode.Emit(OpCodes.Callvirt, ((PropertyInfo)member).GetSetMethod());

            methodCode.MarkLabel(dontRead);
        }

        private static void DeserializeByteArray(TypeBuilder typeBuilder, Type interfaceType, ILGenerator methodCode, PropertyInfo member, LocalBuilder br)
        {
            var array = methodCode.DeclareLocal(typeof(byte[]));
            var arrayLength = methodCode.DeclareLocal(typeof(int));
            var isNull = methodCode.DeclareLocal(typeof(bool));
            var readValue = methodCode.DeclareLocal(typeof(bool));
            var dontRead = methodCode.DefineLabel();

            methodCode.Emit(OpCodes.Ldloc, br); // binary reader
            methodCode.Emit(OpCodes.Callvirt, typeof(BinaryReader).GetMethod("ReadBoolean"));
            methodCode.Emit(OpCodes.Stloc, isNull);
            methodCode.Emit(OpCodes.Ldloc, isNull);
            methodCode.Emit(OpCodes.Ldc_I4_0);
            methodCode.Emit(OpCodes.Ceq);
            methodCode.Emit(OpCodes.Stloc, readValue);
            methodCode.Emit(OpCodes.Ldloc, readValue);
            methodCode.Emit(OpCodes.Brfalse_S, dontRead);
            methodCode.Emit(OpCodes.Ldloc, br);
            methodCode.Emit(OpCodes.Callvirt, typeof(BinaryReader).GetMethod("ReadInt32"));
            methodCode.Emit(OpCodes.Stloc, arrayLength);

            methodCode.Emit(OpCodes.Ldloc, br);
            methodCode.Emit(OpCodes.Ldloc, arrayLength);
            methodCode.Emit(OpCodes.Callvirt, typeof(BinaryReader).GetMethod("ReadBytes"));
            methodCode.Emit(OpCodes.Stloc, array);

            methodCode.Emit(OpCodes.Ldarg_0);
            methodCode.Emit(OpCodes.Ldloc, array);
            
            methodCode.Emit(OpCodes.Callvirt, ((PropertyInfo)member).SetMethod);
            methodCode.MarkLabel(dontRead);
        }

        private static void DeserializeNullableValueType(TypeBuilder typeBuilder, Type interfaceType, ILGenerator methodCode, PropertyInfo member, LocalBuilder br)
        {
            /*

            IL_003d:  ldloc.2
            IL_003e:  callvirt   instance bool [mscorlib]System.IO.BinaryReader::ReadBoolean()
            IL_0043:  stloc.3
            IL_0044:  ldloc.3
            IL_0045:  ldc.i4.0
            IL_0046:  ceq
            IL_0048:  stloc.s    V_6
            IL_004a:  ldloc.s    V_6
            IL_004c:  brfalse.s  IL_0062
            IL_004e:  nop
            IL_004f:  ldloc.0
            IL_0050:  ldloc.2
            IL_0051:  callvirt   instance bool [mscorlib]System.IO.BinaryReader::ReadBoolean()
            IL_0056:  newobj     instance void valuetype [mscorlib]System.Nullable`1<bool>::.ctor(!0)
            IL_005b:  callvirt   instance void 'Altus.Suffūz.Tests'.SimplePOCO::set_nA(valuetype [mscorlib]System.Nullable`1<bool>)
            IL_0060:  nop

            */
            var type = member.PropertyType;
            var isNull = methodCode.DeclareLocal(typeof(bool));
            var readValue = methodCode.DeclareLocal(typeof(bool));
            var dontRead = methodCode.DefineLabel();
            var valueType = type.GetGenericArguments()[0];
            var baseType = valueType;

            if (valueType.IsEnum)
            {
                baseType = valueType.GetFields()[0].FieldType;
            }

            methodCode.Emit(OpCodes.Ldloc, br); // binary reader
            methodCode.Emit(OpCodes.Callvirt, typeof(BinaryReader).GetMethod("ReadBoolean"));
            methodCode.Emit(OpCodes.Stloc, isNull);
            methodCode.Emit(OpCodes.Ldloc, isNull);
            methodCode.Emit(OpCodes.Ldc_I4_1);
            methodCode.Emit(OpCodes.Ceq);
            methodCode.Emit(OpCodes.Stloc, readValue);
            methodCode.Emit(OpCodes.Ldloc, readValue);
            methodCode.Emit(OpCodes.Brfalse_S, dontRead);

            methodCode.Emit(OpCodes.Ldarg_0); // object to write
            methodCode.Emit(OpCodes.Ldloc, br); // binary reader
            if (valueType == typeof(bool))
            {
                methodCode.Emit(OpCodes.Callvirt, typeof(BinaryReader).GetMethod("ReadBoolean"));
            }
            else if (valueType == typeof(byte) || valueType == typeof(byte))
            {
                methodCode.Emit(OpCodes.Callvirt, typeof(BinaryReader).GetMethod("ReadByte"));
            }
            else if (valueType == typeof(sbyte) || valueType == typeof(sbyte))
            {
                methodCode.Emit(OpCodes.Callvirt, typeof(BinaryReader).GetMethod("ReadSByte"));
            }
            else if (valueType == typeof(char) || valueType == typeof(char))
            {
                methodCode.Emit(OpCodes.Callvirt, typeof(BinaryReader).GetMethod("ReadChar"));
            }
            else if (valueType == typeof(short) || valueType == typeof(short))
            {
                methodCode.Emit(OpCodes.Callvirt, typeof(BinaryReader).GetMethod("ReadInt16"));
            }
            else if (valueType == typeof(ushort) || valueType == typeof(ushort))
            {
                methodCode.Emit(OpCodes.Callvirt, typeof(BinaryReader).GetMethod("ReadUInt16"));
            }
            else if (valueType == typeof(int) || valueType == typeof(int))
            {
                methodCode.Emit(OpCodes.Callvirt, typeof(BinaryReader).GetMethod("ReadInt32"));
            }
            else if (valueType == typeof(uint) || valueType == typeof(uint))
            {
                methodCode.Emit(OpCodes.Callvirt, typeof(BinaryReader).GetMethod("ReadUInt32"));
            }
            else if (valueType == typeof(long) || valueType == typeof(long))
            {
                methodCode.Emit(OpCodes.Callvirt, typeof(BinaryReader).GetMethod("ReadInt64"));
            }
            else if (valueType == typeof(ulong))
            {
                methodCode.Emit(OpCodes.Callvirt, typeof(BinaryReader).GetMethod("ReadUInt64"));
            }
            else if (valueType == typeof(float))
            {
                methodCode.Emit(OpCodes.Callvirt, typeof(BinaryReader).GetMethod("ReadSingle"));
            }
            else if (valueType == typeof(double))
            {
                methodCode.Emit(OpCodes.Callvirt, typeof(BinaryReader).GetMethod("ReadDouble"));
            }
            else if (valueType == typeof(decimal))
            {
                methodCode.Emit(OpCodes.Callvirt, typeof(BinaryReader).GetMethod("ReadDecimal"));
            }
            else if (valueType.IsEnum)
            {
                methodCode.Emit(OpCodes.Callvirt, typeof(BinaryReader).GetMethod("ReadInt32"));
            }
            methodCode.Emit(OpCodes.Newobj, type.GetConstructor(new Type[] { valueType }));

            
            methodCode.Emit(OpCodes.Callvirt, ((PropertyInfo)member).SetMethod);

            methodCode.MarkLabel(dontRead);
            methodCode.Emit(OpCodes.Nop);
        }

        private static void DeserializeValueType(TypeBuilder typeBuilder, Type interfaceType, ILGenerator methodCode, PropertyInfo member, LocalBuilder br)
        {
            var type = member.PropertyType;
            var baseType = type;

            if (type.IsEnum)
            {
                baseType = type.GetFields()[0].FieldType;
            }

            methodCode.Emit(OpCodes.Ldarg_0);
            methodCode.Emit(OpCodes.Ldloc, br);

            if (type == typeof(bool))
            {
                methodCode.Emit(OpCodes.Callvirt, typeof(BinaryReader).GetMethod("ReadBoolean"));
            }
            else if (type == typeof(byte) || baseType == typeof(byte))
            {
                methodCode.Emit(OpCodes.Callvirt, typeof(BinaryReader).GetMethod("ReadByte"));
            }
            else if (type == typeof(sbyte) || baseType == typeof(sbyte))
            {
                methodCode.Emit(OpCodes.Callvirt, typeof(BinaryReader).GetMethod("ReadSByte"));
            }
            else if (type == typeof(char) || baseType == typeof(char))
            {
                methodCode.Emit(OpCodes.Callvirt, typeof(BinaryReader).GetMethod("ReadChar"));
            }
            else if (type == typeof(short) || baseType == typeof(short))
            {
                methodCode.Emit(OpCodes.Callvirt, typeof(BinaryReader).GetMethod("ReadInt16"));
            }
            else if (type == typeof(ushort) || baseType == typeof(ushort))
            {
                methodCode.Emit(OpCodes.Callvirt, typeof(BinaryReader).GetMethod("ReadUInt16"));
            }
            else if (type == typeof(int) || baseType == typeof(int))
            {
                methodCode.Emit(OpCodes.Callvirt, typeof(BinaryReader).GetMethod("ReadInt32"));
            }
            else if (type == typeof(uint) || baseType == typeof(uint))
            {
                methodCode.Emit(OpCodes.Callvirt, typeof(BinaryReader).GetMethod("ReadUInt32"));
            }
            else if (type == typeof(long) || baseType == typeof(long))
            {
                methodCode.Emit(OpCodes.Callvirt, typeof(BinaryReader).GetMethod("ReadInt64"));
            }
            else if (type == typeof(ulong) || baseType == typeof(ulong))
            {
                methodCode.Emit(OpCodes.Callvirt, typeof(BinaryReader).GetMethod("ReadUInt64"));
            }
            else if (type == typeof(float))
            {
                methodCode.Emit(OpCodes.Callvirt, typeof(BinaryReader).GetMethod("ReadSingle"));
            }
            else if (type == typeof(double))
            {
                methodCode.Emit(OpCodes.Callvirt, typeof(BinaryReader).GetMethod("ReadDouble"));
            }
            else if (type == typeof(decimal))
            {
                methodCode.Emit(OpCodes.Callvirt, typeof(BinaryReader).GetMethod("ReadDecimal"));
            }
            else if (type.IsEnum)
            {
                methodCode.Emit(OpCodes.Callvirt, typeof(BinaryReader).GetMethod("ReadInt32"));
            }

            methodCode.Emit(OpCodes.Callvirt, ((PropertyInfo)member).SetMethod);

        }

        private static void CheckStreamPosition(ILGenerator methodCode, Label exit, LocalBuilder br)
        {
            /*
            C# --------------------------------------------------------------------------------------------------
            if (reader.BaseStream.Position >= reader.BaseStream.Length) return

            IL --------------------------------------------------------------------------------------------------
            IL_0011:  ldloc.1
            IL_0012:  callvirt   instance class [mscorlib]System.IO.Stream [mscorlib]System.IO.BinaryReader::get_BaseStream()
            IL_0017:  callvirt   instance int64 [mscorlib]System.IO.Stream::get_Position()
            IL_001c:  ldloc.1
            IL_001d:  callvirt   instance class [mscorlib]System.IO.Stream [mscorlib]System.IO.BinaryReader::get_BaseStream()
            IL_0022:  callvirt   instance int64 [mscorlib]System.IO.Stream::get_Length()
            IL_0027:  clt
            IL_0029:  ldc.i4.0
            IL_002a:  ceq
            IL_002c:  stloc.s    V_5
            IL_002e:  ldloc.s    V_5
            IL_0030:  brfalse.s  IL_0037
            IL_0032:  leave      IL_02f8

            */
            var jump = methodCode.DefineLabel();
            methodCode.Emit(OpCodes.Ldloc, br);
            methodCode.Emit(OpCodes.Callvirt, typeof(BinaryReader).GetProperty("BaseStream").GetGetMethod());
            methodCode.Emit(OpCodes.Callvirt, typeof(Stream).GetProperty("Position").GetGetMethod());
            methodCode.Emit(OpCodes.Ldloc, br);
            methodCode.Emit(OpCodes.Callvirt, typeof(BinaryReader).GetProperty("BaseStream").GetGetMethod());
            methodCode.Emit(OpCodes.Callvirt, typeof(Stream).GetProperty("Length").GetGetMethod());
            methodCode.Emit(OpCodes.Clt);
            methodCode.Emit(OpCodes.Ldc_I4_0);
            methodCode.Emit(OpCodes.Ceq);
            methodCode.Emit(OpCodes.Brfalse, jump);
            methodCode.Emit(OpCodes.Leave, exit);
            methodCode.MarkLabel(jump);
        }
    

        private static MethodInfo BuildToBytes(Type interfaceType, TypeBuilder typeBuilder, List<PropertyInfo> props, Type[] additionalInterfaces)
        {
            /*

            C# -------------------------------------------------------------------------------
            public byte[] ToBytes()
            {
                using (var ms = new MemoryStream())
                {
                    using (var bw = new BinaryWriter(ms))
                    {
                        [SERIALIZE MEMBERS.......]
                    }
                    return ms.ToArray();
                }
            }

            IL --------------------------------------------------------------------------------
            .method public hidebysig newslot virtual final 
                    instance uint8[]  ToBytes() cil managed
            {
              // Code size       212 (0xd4)
              .maxstack  3
              .locals init ([0] class [mscorlib]System.IO.MemoryStream ms,
                       [1] class [mscorlib]System.IO.BinaryWriter bw,
                       [2] uint8[] bytes)
              IL_0000:  nop
              IL_0001:  newobj     instance void [mscorlib]System.IO.MemoryStream::.ctor()
              IL_0006:  stloc.0
              .try
              {
                IL_0007:  nop
                IL_0008:  ldloc.0
                IL_0009:  newobj     instance void [mscorlib]System.IO.BinaryWriter::.ctor(class [mscorlib]System.IO.Stream)
                IL_000e:  stloc.1
                .try
                {
                    [SERIALIZE MEMBERS......]
                }  // end .try
                finally
                {
                  IL_00b1:  ldloc.1
                  IL_00b2:  brfalse.s  IL_00bb
                  IL_00b4:  ldloc.1
                  IL_00b5:  callvirt   instance void [mscorlib]System.IDisposable::Dispose()
                  IL_00ba:  nop
                  IL_00bb:  endfinally
                }  // end handler
                IL_00bc:  ldloc.0
                IL_00bd:  callvirt   instance uint8[] [mscorlib]System.IO.MemoryStream::ToArray()
                IL_00c2:  stloc.s    V_5
                IL_00c4:  leave.s    IL_00d1
              }  // end .try
              finally
              {
                IL_00c6:  ldloc.0
                IL_00c7:  brfalse.s  IL_00d0
                IL_00c9:  ldloc.0
                IL_00ca:  callvirt   instance void [mscorlib]System.IDisposable::Dispose()
                IL_00cf:  nop
                IL_00d0:  endfinally
              }  // end handler
              IL_00d1:  ldloc.s    V_5
              IL_00d3:  ret
            } // end of method EmittedModel::ToBytes

            */


            var method = typeBuilder.DefineMethod("ToBytes",
                MethodAttributes.Public
                | MethodAttributes.HideBySig
                | MethodAttributes.NewSlot
                | MethodAttributes.Virtual
                | MethodAttributes.Final,
                CallingConventions.Standard,
                typeof(byte[]),
                Type.EmptyTypes);
            var code = method.GetILGenerator();
            var exit = code.DefineLabel();
            var endfinally1 = code.DefineLabel();
            var endfinally2 = code.DefineLabel();

            var ms = code.DeclareLocal(typeof(MemoryStream));
            var bw = code.DeclareLocal(typeof(BinaryWriter));
            var bytes = code.DeclareLocal(typeof(byte[]));

            code.Emit(OpCodes.Newobj, typeof(MemoryStream).GetConstructor(new Type[0]));
            code.Emit(OpCodes.Stloc_0);

            code.BeginExceptionBlock();
            code.Emit(OpCodes.Ldloc_0);
            code.Emit(OpCodes.Newobj, typeof(BinaryWriter).GetConstructor(new Type[] { typeof(Stream) }));
            code.Emit(OpCodes.Stloc_1);
            code.BeginExceptionBlock();

            SerializeMembers(typeBuilder, interfaceType, code, exit, bw, props, additionalInterfaces);

            code.BeginFinallyBlock();
            code.Emit(OpCodes.Ldloc_1);
            code.Emit(OpCodes.Brfalse_S, endfinally2);
            code.Emit(OpCodes.Ldloc_1);
            code.Emit(OpCodes.Callvirt, typeof(IDisposable).GetMethod("Dispose"));
            code.MarkLabel(endfinally2);
            code.EndExceptionBlock();

            code.Emit(OpCodes.Ldloc_0);
            code.Emit(OpCodes.Callvirt, typeof(MemoryStream).GetMethod("ToArray"));
            code.Emit(OpCodes.Stloc_2);
            code.Emit(OpCodes.Leave, exit);

            code.BeginFinallyBlock();
            code.Emit(OpCodes.Ldloc_0);
            code.Emit(OpCodes.Brfalse, endfinally1);
            code.Emit(OpCodes.Ldloc_0);
            code.Emit(OpCodes.Callvirt, typeof(IDisposable).GetMethod("Dispose"));
            code.MarkLabel(endfinally1);
            code.EndExceptionBlock();

            code.MarkLabel(exit);
            code.Emit(OpCodes.Ldloc_2);
            code.Emit(OpCodes.Ret);

            typeBuilder.DefineMethodOverride(method, typeof(IBinarySerializable).GetMethod("ToBytes"));

            return method;
        }

        class PropertySorter : IComparer<PropertyInfo>
        {
            public PropertySorter(Type interfaceType, Type[] additionalInterfaces)
            {
                this.InterfaceType = interfaceType;
                this.Attributes = new Dictionary<string, BinarySerializableAttribute>();
                this.AdditionalInterfaces = additionalInterfaces;

                var interfaces = new List<Type>(additionalInterfaces);
                interfaces.Insert(0, InterfaceType);
                foreach(var iface in interfaces)
                foreach(var prop in iface.GetPublicProperties().Select(p => new KeyValuePair<string, BinarySerializableAttribute>(p.Name, p.GetCustomAttribute<BinarySerializableAttribute>()))
                                         .Where(a => a.Value != null))
                {
                    Attributes.Add(prop.Key, prop.Value);
                }
                
            }

            public List<PropertyInfo> Filter(IEnumerable<PropertyInfo> props)
            {
                var list = new List<PropertyInfo>();
                foreach(var prop in props)
                {
                    if (Attributes.ContainsKey(prop.Name))
                        list.Add(prop);
                }
                return list;
            }

            public Type InterfaceType { get; private set; }

            public Dictionary<string, BinarySerializableAttribute> Attributes { get; set; }
            public Type[] AdditionalInterfaces { get; private set; }

            public int Compare(PropertyInfo x, PropertyInfo y)
            {
                var a1 = Attributes[x.Name];
                var a2 = Attributes[y.Name];
                return a1.SortOrder.CompareTo(a2.SortOrder);
            }
        }

        private static void SerializeMembers(TypeBuilder typeBuilder, Type interfaceType, ILGenerator methodCode, Label exit, LocalBuilder bw, List<PropertyInfo> props, Type[] additionalInterfaces)
        {
            var sorter = new PropertySorter(interfaceType, additionalInterfaces);
            var serializables = sorter.Filter(props);
            serializables.Sort(sorter);

            foreach (var member in serializables)
            {
                if (IsValueType(member))
                {
                    SerializeValueType(typeBuilder, interfaceType, methodCode, member, bw);
                }
                else if (IsNullableValueType(member))
                {
                    SerializeNullableValueType(typeBuilder, interfaceType, methodCode, member, bw);
                }
                else
                {
                    var memberType = member.PropertyType;
                    if (memberType == typeof(byte[]))
                    {
                        SerializeByteArray(typeBuilder, interfaceType, methodCode, member, bw);
                    }
                    else if (memberType == typeof(char[]))
                    {
                        SerializeCharArray(typeBuilder, interfaceType, methodCode, member, bw);
                    }
                    else if (memberType == typeof(DateTime))
                    {
                        SerializeDateTime(typeBuilder, interfaceType, methodCode, member, bw);
                    }
                    else if (memberType == typeof(string))
                    {
                        SerializeString(typeBuilder, interfaceType, methodCode, member, bw);
                    }
                    else if (memberType == typeof(DateTime?))
                    {
                        SerializeNullableDateTime(typeBuilder, interfaceType, methodCode, member, bw);
                    }
                    else if (memberType.IsArray && !memberType.GetElementType().Implements<IModel>())
                    {
                        SerializeArray(typeBuilder, interfaceType, methodCode, member, bw);
                    }
                    else if (memberType.Implements<IModel>())
                    {
                        SerializeModel(typeBuilder, interfaceType, methodCode, member, bw);
                    }
                    else if (memberType.Implements(typeof(IEnumerable<>)) 
                        && ((memberType.IsGenericType && memberType.GetGenericArguments()[0].Implements<IModel>()) || (memberType.IsArray && memberType.GetElementType().Implements<IModel>())))
                    {
                        SerializeEnumerableModels(typeBuilder, interfaceType, methodCode, member, bw);
                    }
                    else
                    {
                        SerializeObject(typeBuilder, interfaceType, methodCode, member, bw);
                    }
                }
            }
        }

        private static void SerializeEnumerableModels(TypeBuilder typeBuilder, Type interfaceType, ILGenerator methodCode, PropertyInfo member, LocalBuilder bw)
        {
            if (member.PropertyType.IsArray)
            {
                SerializeArrayModels(typeBuilder, interfaceType, methodCode, member, bw);
            }
            else if (member.PropertyType.Implements<ICollection>())
            {
                SerializeListModels(typeBuilder, interfaceType, methodCode, member, bw);
            }
            else
                throw new NotSupportedException("The enumerable IModel collection type is not supported.  The must either be an Array or ICollection.");
        }

        private static void SerializeArrayModels(TypeBuilder typeBuilder, Type interfaceType, ILGenerator methodCode, PropertyInfo member, LocalBuilder bw)
        {
            /*

            IL_00da:  ldarg.0
            IL_00db:  call       instance class Data.Core.Compilation.IChild[] Data.Core.Compilation.EmittedModel::get_Children()
            IL_00e0:  ldnull
            IL_00e1:  cgt.un
            IL_00e3:  stloc.s    childrenIsNotNull
            IL_00e5:  ldloc.1
            IL_00e6:  ldloc.s    childrenIsNotNull
            IL_00e8:  callvirt   instance void [mscorlib]System.IO.BinaryWriter::Write(bool)
            IL_00ed:  nop
            IL_00ee:  ldloc.s    childrenIsNotNull
            IL_00f0:  stloc.s    V_10
            IL_00f2:  ldloc.s    V_10
            IL_00f4:  brfalse.s  IL_0153
            IL_00f6:  nop
            IL_00f7:  ldloc.1
            IL_00f8:  ldarg.0
            IL_00f9:  call       instance class Data.Core.Compilation.IChild[] Data.Core.Compilation.EmittedModel::get_Children()
            IL_00fe:  ldlen
            IL_00ff:  conv.i4
            IL_0100:  callvirt   instance void [mscorlib]System.IO.BinaryWriter::Write(int32)
            IL_0105:  nop
            IL_0106:  ldc.i4.0
            IL_0107:  stloc.s    i
            IL_0109:  br.s       IL_0140
            IL_010b:  nop
            IL_010c:  ldarg.0
            IL_010d:  call       instance class Data.Core.Compilation.IChild[] Data.Core.Compilation.EmittedModel::get_Children()
            IL_0112:  ldloc.s    i
            IL_0114:  ldelem.ref
            IL_0115:  castclass  [Common]Common.Serialization.Binary.IBinarySerializable
            IL_011a:  callvirt   instance uint8[] [Common]Common.Serialization.Binary.IBinarySerializable::ToBytes()
            IL_011f:  stloc.s    bytes
            IL_0121:  ldloc.1
            IL_0122:  ldloc.s    bytes
            IL_0124:  ldlen
            IL_0125:  conv.i4
            IL_0126:  callvirt   instance void [mscorlib]System.IO.BinaryWriter::Write(int32)
            IL_012b:  nop
            IL_012c:  ldloc.1
            IL_012d:  ldloc.s    bytes
            IL_012f:  callvirt   instance void [mscorlib]System.IO.BinaryWriter::Write(uint8[])
            IL_0134:  nop
            IL_0135:  nop
            IL_0136:  ldloc.s    i
            IL_0138:  stloc.s    V_13
            IL_013a:  ldloc.s    V_13
            IL_013c:  ldc.i4.1
            IL_013d:  add
            IL_013e:  stloc.s    i
            IL_0140:  ldloc.s    i
            IL_0142:  ldarg.0
            IL_0143:  call       instance class Data.Core.Compilation.IChild[] Data.Core.Compilation.EmittedModel::get_Children()
            IL_0148:  ldlen
            IL_0149:  conv.i4
            IL_014a:  clt
            IL_014c:  stloc.s    V_14
            IL_014e:  ldloc.s    V_14
            IL_0150:  brtrue.s   IL_010b
            IL_0152:  nop

            */
            var propIsNotNull = methodCode.DeclareLocal(typeof(bool));
            var i = methodCode.DeclareLocal(typeof(int));
            var bytes = methodCode.DeclareLocal(typeof(byte[]));
            var propIsNull = methodCode.DefineLabel();
            var loopTest = methodCode.DefineLabel();
            var loopStart = methodCode.DefineLabel();
            

            methodCode.Emit(OpCodes.Ldarg_0);
            methodCode.Emit(OpCodes.Call, member.GetMethod);
            methodCode.Emit(OpCodes.Ldnull);
            methodCode.Emit(OpCodes.Cgt_Un);
            methodCode.Emit(OpCodes.Stloc, propIsNotNull);
            methodCode.Emit(OpCodes.Ldloc, bw);
            methodCode.Emit(OpCodes.Ldloc, propIsNotNull);
            methodCode.Emit(OpCodes.Callvirt, typeof(BinaryWriter).GetMethod("Write", new Type[] { typeof(bool) }));
            methodCode.Emit(OpCodes.Ldloc, propIsNotNull);
            methodCode.Emit(OpCodes.Brfalse_S, propIsNull);
            methodCode.Emit(OpCodes.Ldloc, bw);
            methodCode.Emit(OpCodes.Ldarg_0);
            methodCode.Emit(OpCodes.Call, member.GetMethod);
            methodCode.Emit(OpCodes.Ldlen);
            methodCode.Emit(OpCodes.Conv_I4);
            methodCode.Emit(OpCodes.Callvirt, typeof(BinaryWriter).GetMethod("Write", new Type[] { typeof(int) }));
            methodCode.Emit(OpCodes.Ldc_I4_0);
            methodCode.Emit(OpCodes.Stloc, i);
            methodCode.Emit(OpCodes.Br_S, loopTest);
            methodCode.MarkLabel(loopStart);
            methodCode.Emit(OpCodes.Ldarg_0);
            methodCode.Emit(OpCodes.Call, member.GetMethod);
            methodCode.Emit(OpCodes.Ldloc, i);
            methodCode.Emit(OpCodes.Ldelem_Ref);
            methodCode.Emit(OpCodes.Castclass, typeof(IBinarySerializable));
            methodCode.Emit(OpCodes.Callvirt, typeof(IBinarySerializable).GetMethod("ToBytes"));
            methodCode.Emit(OpCodes.Stloc, bytes);
            methodCode.Emit(OpCodes.Ldloc, bw);
            methodCode.Emit(OpCodes.Ldloc, bytes);
            methodCode.Emit(OpCodes.Ldlen);
            methodCode.Emit(OpCodes.Conv_I4);
            methodCode.Emit(OpCodes.Callvirt, typeof(BinaryWriter).GetMethod("Write", new Type[] { typeof(int) }));
            methodCode.Emit(OpCodes.Ldloc, bw);
            methodCode.Emit(OpCodes.Ldloc, bytes);
            methodCode.Emit(OpCodes.Callvirt, typeof(BinaryWriter).GetMethod("Write", new Type[] { typeof(byte[]) }));
            methodCode.Emit(OpCodes.Ldloc, i);
            methodCode.Emit(OpCodes.Ldc_I4_1);
            methodCode.Emit(OpCodes.Add);
            methodCode.Emit(OpCodes.Stloc, i);

            methodCode.MarkLabel(loopTest);
            methodCode.Emit(OpCodes.Ldloc, i);
            methodCode.Emit(OpCodes.Ldarg_0);
            methodCode.Emit(OpCodes.Call, member.GetMethod);
            methodCode.Emit(OpCodes.Ldlen);
            methodCode.Emit(OpCodes.Conv_I4);
            methodCode.Emit(OpCodes.Clt);
            methodCode.Emit(OpCodes.Brtrue_S, loopStart);

            methodCode.MarkLabel(propIsNull);

        }

        private static void SerializeListModels(TypeBuilder typeBuilder, Type interfaceType, ILGenerator methodCode, PropertyInfo member, LocalBuilder bw)
        {
            /*


            IL_0153:  ldarg.0
            IL_0154:  call       instance class [mscorlib]System.Collections.Generic.List`1<class Data.Core.Compilation.IChild> Data.Core.Compilation.EmittedModel::get_MoreChildren()
            IL_0159:  ldnull
            IL_015a:  cgt.un
            IL_015c:  stloc.s    moreChildrenIsNotNull
            IL_015e:  ldloc.1
            IL_015f:  ldloc.s    moreChildrenIsNotNull
            IL_0161:  callvirt   instance void [mscorlib]System.IO.BinaryWriter::Write(bool)
            IL_0166:  nop
            IL_0167:  ldloc.s    moreChildrenIsNotNull
            IL_0169:  stloc.s    V_16
            IL_016b:  ldloc.s    V_16
            IL_016d:  brfalse.s  IL_01d6
            IL_016f:  nop
            IL_0170:  ldloc.1
            IL_0171:  ldarg.0
            IL_0172:  call       instance class [mscorlib]System.Collections.Generic.List`1<class Data.Core.Compilation.IChild> Data.Core.Compilation.EmittedModel::get_MoreChildren()
            IL_0177:  callvirt   instance int32 [mscorlib]System.Collections.ICollection::get_Count()
            IL_017c:  callvirt   instance void [mscorlib]System.IO.BinaryWriter::Write(int32)
            IL_0181:  nop
            IL_0182:  ldc.i4.0
            IL_0183:  stloc.s    V_17
            IL_0185:  br.s       IL_01c0
            IL_0187:  nop
            IL_0188:  ldarg.0
            IL_0189:  call       instance class [mscorlib]System.Collections.Generic.List`1<class Data.Core.Compilation.IChild> Data.Core.Compilation.EmittedModel::get_MoreChildren()
            IL_018e:  ldloc.s    V_17
            IL_0190:  callvirt   instance !0 class [mscorlib]System.Collections.Generic.List`1<class Data.Core.Compilation.IChild>::get_Item(int32)
            IL_0195:  castclass  [Common]Common.Serialization.Binary.IBinarySerializable
            IL_019a:  callvirt   instance uint8[] [Common]Common.Serialization.Binary.IBinarySerializable::ToBytes()
            IL_019f:  stloc.s    V_18
            IL_01a1:  ldloc.1
            IL_01a2:  ldloc.s    V_18
            IL_01a4:  ldlen
            IL_01a5:  conv.i4
            IL_01a6:  callvirt   instance void [mscorlib]System.IO.BinaryWriter::Write(int32)
            IL_01ab:  nop
            IL_01ac:  ldloc.1
            IL_01ad:  ldloc.s    V_18
            IL_01af:  callvirt   instance void [mscorlib]System.IO.BinaryWriter::Write(uint8[])
            IL_01b4:  nop
            IL_01b5:  nop
            IL_01b6:  ldloc.s    V_17
            IL_01b8:  stloc.s    V_14
            IL_01ba:  ldloc.s    V_14
            IL_01bc:  ldc.i4.1
            IL_01bd:  add
            IL_01be:  stloc.s    V_17
            IL_01c0:  ldloc.s    V_17
            IL_01c2:  ldarg.0
            IL_01c3:  call       instance class [mscorlib]System.Collections.Generic.List`1<class Data.Core.Compilation.IChild> Data.Core.Compilation.EmittedModel::get_MoreChildren()
            IL_01c8:  callvirt   instance int32 [mscorlib]System.Collections.ICollection::get_Count()
            IL_01cd:  clt
            IL_01cf:  stloc.s    V_19
            IL_01d1:  ldloc.s    V_19
            IL_01d3:  brtrue.s   IL_0187

            */

            var propIsNotNull = methodCode.DeclareLocal(typeof(bool));
            var i = methodCode.DeclareLocal(typeof(int));
            var bytes = methodCode.DeclareLocal(typeof(byte[]));
            var propIsNull = methodCode.DefineLabel();
            var loopTest = methodCode.DefineLabel();
            var loopStart = methodCode.DefineLabel();


            methodCode.Emit(OpCodes.Ldarg_0);
            methodCode.Emit(OpCodes.Call, member.GetMethod);
            methodCode.Emit(OpCodes.Ldnull);
            methodCode.Emit(OpCodes.Cgt_Un);
            methodCode.Emit(OpCodes.Stloc, propIsNotNull);
            methodCode.Emit(OpCodes.Ldloc, bw);
            methodCode.Emit(OpCodes.Ldloc, propIsNotNull);
            methodCode.Emit(OpCodes.Callvirt, typeof(BinaryWriter).GetMethod("Write", new Type[] { typeof(bool) }));
            methodCode.Emit(OpCodes.Ldloc, propIsNotNull);
            methodCode.Emit(OpCodes.Brfalse_S, propIsNull);
            methodCode.Emit(OpCodes.Ldloc, bw);
            methodCode.Emit(OpCodes.Ldarg_0);
            methodCode.Emit(OpCodes.Call, member.GetMethod);
            methodCode.Emit(OpCodes.Callvirt, typeof(ICollection).GetProperty("Count").GetMethod);
            methodCode.Emit(OpCodes.Callvirt, typeof(BinaryWriter).GetMethod("Write", new Type[] { typeof(int) }));
            methodCode.Emit(OpCodes.Ldc_I4_0);
            methodCode.Emit(OpCodes.Stloc, i);
            methodCode.Emit(OpCodes.Br_S, loopTest);
            methodCode.MarkLabel(loopStart);
            methodCode.Emit(OpCodes.Ldarg_0);
            methodCode.Emit(OpCodes.Call, member.GetMethod);
            methodCode.Emit(OpCodes.Castclass, typeof(IList));
            methodCode.Emit(OpCodes.Ldloc, i);
            methodCode.Emit(OpCodes.Callvirt, typeof(IList).GetMethod("get_Item"));
            methodCode.Emit(OpCodes.Castclass, typeof(IBinarySerializable));
            methodCode.Emit(OpCodes.Callvirt, typeof(IBinarySerializable).GetMethod("ToBytes"));
            methodCode.Emit(OpCodes.Stloc, bytes);
            methodCode.Emit(OpCodes.Ldloc, bw);
            methodCode.Emit(OpCodes.Ldloc, bytes);
            methodCode.Emit(OpCodes.Ldlen);
            methodCode.Emit(OpCodes.Conv_I4);
            methodCode.Emit(OpCodes.Callvirt, typeof(BinaryWriter).GetMethod("Write", new Type[] { typeof(int) }));
            methodCode.Emit(OpCodes.Ldloc, bw);
            methodCode.Emit(OpCodes.Ldloc, bytes);
            methodCode.Emit(OpCodes.Callvirt, typeof(BinaryWriter).GetMethod("Write", new Type[] { typeof(byte[]) }));
            methodCode.Emit(OpCodes.Ldloc, i);
            methodCode.Emit(OpCodes.Ldc_I4_1);
            methodCode.Emit(OpCodes.Add);
            methodCode.Emit(OpCodes.Stloc, i);

            methodCode.MarkLabel(loopTest);
            methodCode.Emit(OpCodes.Ldloc, i);
            methodCode.Emit(OpCodes.Ldarg_0);
            methodCode.Emit(OpCodes.Call, member.GetMethod);
            methodCode.Emit(OpCodes.Callvirt, typeof(ICollection).GetProperty("Count").GetMethod);
            methodCode.Emit(OpCodes.Clt);
            methodCode.Emit(OpCodes.Brtrue_S, loopStart);

            methodCode.MarkLabel(propIsNull);

        }

        private static void SerializeModel(TypeBuilder typeBuilder, Type interfaceType, ILGenerator methodCode, PropertyInfo member, LocalBuilder bw)
        {

            /*
            var childIsNotNull = Child != null;
            bw.Write(childIsNotNull);
            if (childIsNotNull)
            {
                var childBytes = ((IBinarySerializable)Child).ToBytes();
                bw.Write(childBytes.Length);
                bw.Write(childBytes);
            }

            -------------------------------------------------------------------

            IL_0099:  ldarg.0
            IL_009a:  call       instance class Data.Core.Compilation.IChild Data.Core.Compilation.EmittedModel::get_Child()
            IL_009f:  ldnull
            IL_00a0:  cgt.un
            IL_00a2:  stloc.3
            IL_00a3:  ldloc.1
            IL_00a4:  ldloc.3
            IL_00a5:  callvirt   instance void [mscorlib]System.IO.BinaryWriter::Write(bool)
            IL_00aa:  nop
            IL_00ab:  ldloc.3
            IL_00ac:  stloc.s    V_8
            IL_00ae:  ldloc.s    V_8
            IL_00b0:  brfalse.s  IL_00da
            IL_00b2:  nop
            IL_00b3:  ldarg.0
            IL_00b4:  call       instance class Data.Core.Compilation.IChild Data.Core.Compilation.EmittedModel::get_Child()
            IL_00b9:  castclass  [Common]Common.Serialization.Binary.IBinarySerializable
            IL_00be:  callvirt   instance uint8[] [Common]Common.Serialization.Binary.IBinarySerializable::ToBytes()
            IL_00c3:  stloc.s    childBytes
            IL_00c5:  ldloc.1
            IL_00c6:  ldloc.s    childBytes
            IL_00c8:  ldlen
            IL_00c9:  conv.i4
            IL_00ca:  callvirt   instance void [mscorlib]System.IO.BinaryWriter::Write(int32)
            IL_00cf:  nop
            IL_00d0:  ldloc.1
            IL_00d1:  ldloc.s    childBytes
            IL_00d3:  callvirt   instance void [mscorlib]System.IO.BinaryWriter::Write(uint8[])


            */
            var isNotNull = methodCode.DeclareLocal(typeof(bool));
            var length = methodCode.DeclareLocal(typeof(int));
            var bytes = methodCode.DeclareLocal(typeof(byte[]));
            var propertyIsNull = methodCode.DefineLabel();
            methodCode.Emit(OpCodes.Ldarg_0);
            methodCode.Emit(OpCodes.Call, member.GetMethod);
            methodCode.Emit(OpCodes.Ldnull);
            methodCode.Emit(OpCodes.Cgt_Un);
            methodCode.Emit(OpCodes.Stloc, isNotNull);
            methodCode.Emit(OpCodes.Ldloc, bw);
            methodCode.Emit(OpCodes.Ldloc, isNotNull);
            methodCode.Emit(OpCodes.Callvirt, typeof(BinaryWriter).GetMethod("Write", new Type[] { typeof(bool) }));
            methodCode.Emit(OpCodes.Ldloc, isNotNull);
            methodCode.Emit(OpCodes.Brfalse_S, propertyIsNull);
            if (member.PropertyType == typeof(IModel) || member.PropertyType == typeof(ILink))
            {
                // we need to include the actual model type so we can instantiate it when deserializing
                /*
                ldlocl, bw
                ldarg.0
                IL_0012:  call instance class [mscorlib] System.Type Data.Core.Compilation.EmittedModel::get_ModelType()
                IL_0017:  call string Data.Core.ModelTypeManager::GetModelName(class [mscorlib] System.Type)
                IL_001c:  callvirt instance void[mscorlib] System.IO.BinaryWriter::Write(string)
                IL_0021:  nop
                */
                methodCode.Emit(OpCodes.Ldloc, bw);
                methodCode.Emit(OpCodes.Ldarg_0);
                methodCode.Emit(OpCodes.Call, member.GetMethod);
                methodCode.Emit(OpCodes.Call, typeof(IModel).GetProperty("ModelType").GetMethod);
                var getModelName = typeof(ModelTypeManager).GetMethods(BindingFlags.Public | BindingFlags.Static).First(mi => mi.Name.Equals("GetModelName") && !mi.IsGenericMethod);
                methodCode.Emit(OpCodes.Call, getModelName);
                methodCode.Emit(OpCodes.Callvirt, typeof(BinaryWriter).GetMethod("Write", new Type[] { typeof(string) }));
            }
            methodCode.Emit(OpCodes.Ldarg_0);
            methodCode.Emit(OpCodes.Call, member.GetMethod);
            methodCode.Emit(OpCodes.Castclass, typeof(IBinarySerializable));
            methodCode.Emit(OpCodes.Callvirt, typeof(IBinarySerializable).GetMethod("ToBytes"));
            methodCode.Emit(OpCodes.Stloc, bytes);
            methodCode.Emit(OpCodes.Ldloc, bw);
            methodCode.Emit(OpCodes.Ldloc, bytes);
            methodCode.Emit(OpCodes.Ldlen);
            methodCode.Emit(OpCodes.Conv_I4);
            methodCode.Emit(OpCodes.Callvirt, typeof(BinaryWriter).GetMethod("Write", new Type[] { typeof(int) }));
            methodCode.Emit(OpCodes.Ldloc, bw);
            methodCode.Emit(OpCodes.Ldloc, bytes);
            methodCode.Emit(OpCodes.Callvirt, typeof(BinaryWriter).GetMethod("Write", new Type[] { typeof(byte[]) }));

            methodCode.MarkLabel(propertyIsNull);

        }

        private static void SerializeObject(TypeBuilder typeBuilder, Type interfaceType, ILGenerator methodCode, PropertyInfo member, LocalBuilder bw)
        {
            var type = member.PropertyType;
            var value = methodCode.DeclareLocal(type);
            var hasValue = methodCode.DeclareLocal(typeof(bool));
            var noValue = methodCode.DefineLabel();
            methodCode.Emit(OpCodes.Ldarg_0); // object to read

            methodCode.Emit(OpCodes.Callvirt, ((PropertyInfo)member).GetMethod);

            methodCode.Emit(OpCodes.Stloc, value);
            methodCode.Emit(OpCodes.Ldloc, value);
            methodCode.Emit(OpCodes.Ldnull);
            methodCode.Emit(OpCodes.Cgt_Un);
            methodCode.Emit(OpCodes.Stloc, hasValue);
            methodCode.Emit(OpCodes.Ldloc, bw); // binary writer
            methodCode.Emit(OpCodes.Ldloc, hasValue);
            methodCode.Emit(OpCodes.Callvirt, typeof(BinaryWriter).GetMethod("Write", new Type[] { typeof(bool) }));
            methodCode.Emit(OpCodes.Ldloc, hasValue);
            methodCode.Emit(OpCodes.Brfalse, noValue); // the array is null, don't write anything else

            methodCode.Emit(OpCodes.Ldtoken, type);
            methodCode.Emit(OpCodes.Call, typeof(Type).GetMethod("GetTypeFromHandle", BindingFlags.Public | BindingFlags.Static));
            methodCode.Emit(OpCodes.Ldloc, value);
            methodCode.Emit(OpCodes.Ldloc, bw); // binary writer
            methodCode.Emit(OpCodes.Call, typeof(Serialization.Binary._BinarySerializer).GetMethod("Serialize", BindingFlags.Public | BindingFlags.Static));

            methodCode.MarkLabel(noValue);
        }

        private static void SerializeArray(TypeBuilder typeBuilder, Type interfaceType, ILGenerator methodCode, PropertyInfo member, LocalBuilder bw)
        {
            /*

            IL_0016:  ldloc.0
            IL_0017:  callvirt   instance !0[] class 'Altus.Suffūz.Tests'.Array`1<int32>::get_A()
            IL_001c:  stloc.3
            IL_001d:  ldloc.3
            IL_001e:  ldnull
            IL_001f:  cgt.un
            IL_0021:  stloc.s    hasValue
            IL_0023:  ldloc.2
            IL_0024:  ldloc.s    hasValue
            IL_0026:  callvirt   instance void [mscorlib]System.IO.BinaryWriter::Write(bool)
            IL_002b:  nop
            IL_002c:  ldloc.s    hasValue
            IL_002e:  stloc.s    V_5
            IL_0030:  ldloc.s    V_5
            IL_0032:  brfalse.s  IL_006c
            IL_0034:  nop
            IL_0035:  ldloc.3
            IL_0036:  ldlen
            IL_0037:  conv.i4
            IL_0038:  stloc.s    count
            IL_003a:  ldloc.2
            IL_003b:  ldloc.s    count
            IL_003d:  callvirt   instance void [mscorlib]System.IO.BinaryWriter::Write(int32)
            IL_0042:  nop
            IL_0043:  ldc.i4.0
            IL_0044:  stloc.s    i
            IL_0046:  br.s       IL_005f
            IL_0048:  nop
            IL_0049:  ldloc.2
            IL_004a:  ldloc.3
            IL_004b:  ldloc.s    i
            IL_004d:  ldelem.i4
            IL_004e:  callvirt   instance void [mscorlib]System.IO.BinaryWriter::Write(int32)
            IL_0053:  nop
            IL_0054:  nop
            IL_0055:  ldloc.s    i
            IL_0057:  stloc.s    V_8
            IL_0059:  ldloc.s    V_8
            IL_005b:  ldc.i4.1
            IL_005c:  add
            IL_005d:  stloc.s    i
            IL_005f:  ldloc.s    i
            IL_0061:  ldloc.s    count
            IL_0063:  clt
            IL_0065:  stloc.s    V_9
            IL_0067:  ldloc.s    V_9
            IL_0069:  brtrue.s   IL_0048
            IL_006b:  nop


            */

            var type = member.PropertyType;
            var elemType = type.GetElementType();
            var value = methodCode.DeclareLocal(type);
            var hasValue = methodCode.DeclareLocal(typeof(bool));
            var noValue = methodCode.DefineLabel();
            var count = methodCode.DeclareLocal(typeof(int));

            methodCode.Emit(OpCodes.Ldarg_0); // object to read

            methodCode.Emit(OpCodes.Callvirt, ((PropertyInfo)member).GetGetMethod());
            methodCode.Emit(OpCodes.Stloc, value);
            methodCode.Emit(OpCodes.Ldloc, value);
            methodCode.Emit(OpCodes.Ldnull);
            methodCode.Emit(OpCodes.Cgt_Un);
            methodCode.Emit(OpCodes.Stloc, hasValue);
            methodCode.Emit(OpCodes.Ldloc, bw); // binary writer
            methodCode.Emit(OpCodes.Ldloc, hasValue);
            methodCode.Emit(OpCodes.Callvirt, typeof(BinaryWriter).GetMethod("Write", new Type[] { typeof(bool) }));
            methodCode.Emit(OpCodes.Ldloc, hasValue);
            methodCode.Emit(OpCodes.Brfalse, noValue); // the array is null, don't write anything else

            // write array length
            methodCode.Emit(OpCodes.Ldloc, value);
            methodCode.Emit(OpCodes.Ldlen);
            methodCode.Emit(OpCodes.Conv_I4); // i think this is redundant?
            methodCode.Emit(OpCodes.Stloc, count);
            methodCode.Emit(OpCodes.Ldloc, bw);
            methodCode.Emit(OpCodes.Ldloc, count);
            methodCode.Emit(OpCodes.Callvirt, typeof(BinaryWriter).GetMethod("Write", new Type[] { typeof(int) }));

            // loop over array elements
            var i = methodCode.DeclareLocal(typeof(int));
            var checkLoop = methodCode.DefineLabel();
            var topOfLoop = methodCode.DefineLabel();
            methodCode.Emit(OpCodes.Ldc_I4_0);
            methodCode.Emit(OpCodes.Stloc, i);
            methodCode.Emit(OpCodes.Br_S, checkLoop);

            methodCode.MarkLabel(topOfLoop);
            methodCode.Emit(OpCodes.Nop);

            if (IsValueType(elemType))
            {
                SerializeValueTypeArrayElement(methodCode, value, i, elemType, bw);
            }
            else if (IsNullableValueType(elemType))
            {
                SerializeNullableValueTypeArrayElement(methodCode, value, i, elemType, bw);
            }
            else if (elemType == typeof(string))
            {
                SerializeStringArrayElement(methodCode, value, i, elemType, bw);
            }
            else if (elemType == typeof(DateTime))
            {
                SerializeDateTimeArrayElement(methodCode, value, i, elemType, bw);
            }
            else if (elemType == typeof(DateTime?))
            {
                SerializeNullableDateTimeArrayElement(methodCode, value, i, elemType, bw);
            }
            else
            {
                throw new NotSupportedException();
            }

            // increment i
            methodCode.Emit(OpCodes.Ldloc, i);
            methodCode.Emit(OpCodes.Ldc_I4_1);
            methodCode.Emit(OpCodes.Add);
            methodCode.Emit(OpCodes.Stloc, i);
            methodCode.MarkLabel(checkLoop);
            methodCode.Emit(OpCodes.Ldloc, i);
            methodCode.Emit(OpCodes.Ldloc, count);
            methodCode.Emit(OpCodes.Clt);
            methodCode.Emit(OpCodes.Brtrue, topOfLoop);


            methodCode.MarkLabel(noValue);
        }

        private static void SerializeNullableDateTimeArrayElement(ILGenerator methodCode, LocalBuilder array, LocalBuilder i, Type elemType, LocalBuilder bw)
        {
            var value = methodCode.DeclareLocal(elemType);
            var date = methodCode.DeclareLocal(typeof(DateTime));
            var binaryDate = methodCode.DeclareLocal(typeof(long));
            var isNull = methodCode.DeclareLocal(typeof(bool));
            var writeValue = methodCode.DeclareLocal(typeof(bool));
            var dontWrite = methodCode.DefineLabel();

            methodCode.Emit(OpCodes.Ldloc, array);
            methodCode.Emit(OpCodes.Ldloc, i);
            methodCode.Emit(OpCodes.Ldelem, elemType);
            methodCode.Emit(OpCodes.Stloc, value);
            methodCode.Emit(OpCodes.Ldloca, value);

            methodCode.Emit(OpCodes.Call, elemType.GetProperty("HasValue").GetGetMethod());
            methodCode.Emit(OpCodes.Stloc, isNull);
            methodCode.Emit(OpCodes.Ldloc, bw);
            methodCode.Emit(OpCodes.Ldloc, isNull);
            methodCode.Emit(OpCodes.Callvirt, typeof(BinaryWriter).GetMethod("Write", new Type[] { typeof(bool) }));
            methodCode.Emit(OpCodes.Ldloc, isNull);
            methodCode.Emit(OpCodes.Ldc_I4_1);
            methodCode.Emit(OpCodes.Ceq);
            methodCode.Emit(OpCodes.Stloc, writeValue);
            methodCode.Emit(OpCodes.Ldloc, writeValue);
            methodCode.Emit(OpCodes.Brfalse_S, dontWrite);

            methodCode.Emit(OpCodes.Ldloca, value);
            methodCode.Emit(OpCodes.Call, elemType.GetProperty("Value").GetGetMethod());
            methodCode.Emit(OpCodes.Stloc, date);
            methodCode.Emit(OpCodes.Ldloca, date);
            methodCode.Emit(OpCodes.Call, typeof(DateTime).GetMethod("ToBinary", BindingFlags.Public | BindingFlags.Instance));
            methodCode.Emit(OpCodes.Stloc, binaryDate);
            methodCode.Emit(OpCodes.Ldloc, bw); // binary writer
            methodCode.Emit(OpCodes.Ldloc, binaryDate);
            methodCode.Emit(OpCodes.Callvirt, typeof(BinaryWriter).GetMethod("Write", new Type[] { typeof(long) }));

            methodCode.MarkLabel(dontWrite);
            methodCode.Emit(OpCodes.Nop);
        }

        private static void SerializeDateTimeArrayElement(ILGenerator methodCode, LocalBuilder array, LocalBuilder i, Type elemType, LocalBuilder bw)
        {
            var value = methodCode.DeclareLocal(typeof(DateTime));
            methodCode.Emit(OpCodes.Ldloc, bw);
            methodCode.Emit(OpCodes.Ldloc, array);
            methodCode.Emit(OpCodes.Ldloc, i);
            methodCode.Emit(OpCodes.Ldelem, typeof(DateTime));
            methodCode.Emit(OpCodes.Stloc, value);
            methodCode.Emit(OpCodes.Ldloca, value);
            methodCode.Emit(OpCodes.Call, typeof(DateTime).GetMethod("ToBinary", BindingFlags.Public | BindingFlags.Instance));
            methodCode.Emit(OpCodes.Callvirt, typeof(BinaryWriter).GetMethod("Write", new Type[] { typeof(long) }));
        }

        private static void SerializeStringArrayElement(ILGenerator methodCode, LocalBuilder value, LocalBuilder i, Type elemType, LocalBuilder bw)
        {
            /*

            IL_0049:  ldloc.3
            IL_004a:  ldloc.s    i
            IL_004c:  ldelem.ref
            IL_004d:  stloc.s    'value'
            IL_004f:  ldloc.s    'value'
            IL_0051:  ldnull
            IL_0052:  ceq
            IL_0054:  stloc.s    isNull
            IL_0056:  ldloc.2
            IL_0057:  ldloc.s    isNull
            IL_0059:  callvirt   instance void [mscorlib]System.IO.BinaryWriter::Write(bool)
            IL_005e:  nop
            IL_005f:  ldloc.s    isNull
            IL_0061:  ldc.i4.0
            IL_0062:  ceq
            IL_0064:  stloc.s    V_10
            IL_0066:  ldloc.s    V_10
            IL_0068:  brfalse.s  IL_0077
            IL_006a:  nop
            IL_006b:  ldloc.2
            IL_006c:  ldloc.3
            IL_006d:  ldloc.s    i
            IL_006f:  ldelem.ref
            IL_0070:  callvirt   instance void [mscorlib]System.IO.BinaryWriter::Write(string)


            */
            var elemValue = methodCode.DeclareLocal(elemType);
            var isElemNull = methodCode.DeclareLocal(typeof(bool));

            var nullElement = methodCode.DefineLabel();

            methodCode.Emit(OpCodes.Ldloc, bw);
            methodCode.Emit(OpCodes.Ldloc, value);
            methodCode.Emit(OpCodes.Ldloc, i);
            methodCode.Emit(OpCodes.Ldelem_Ref);

            methodCode.Emit(OpCodes.Stloc, elemValue);
            methodCode.Emit(OpCodes.Ldloc, elemValue);
            methodCode.Emit(OpCodes.Ldnull);
            methodCode.Emit(OpCodes.Ceq);

            methodCode.Emit(OpCodes.Stloc, isElemNull);
            methodCode.Emit(OpCodes.Ldloc, isElemNull);
            methodCode.Emit(OpCodes.Callvirt, typeof(BinaryWriter).GetMethod("Write", new Type[] { typeof(bool) }));
            methodCode.Emit(OpCodes.Ldloc, isElemNull);
            methodCode.Emit(OpCodes.Ldc_I4_0);
            methodCode.Emit(OpCodes.Ceq);
            methodCode.Emit(OpCodes.Brfalse, nullElement);

            methodCode.Emit(OpCodes.Ldloc, bw);
            methodCode.Emit(OpCodes.Ldloc, elemValue);
            methodCode.Emit(OpCodes.Callvirt, typeof(BinaryWriter).GetMethod("Write", new Type[] { typeof(string) }));

            methodCode.MarkLabel(nullElement);
        }

        private static void SerializeNullableValueTypeArrayElement(ILGenerator methodCode, LocalBuilder array, LocalBuilder i, Type elemType, LocalBuilder bw)
        {
            var value = methodCode.DeclareLocal(elemType);
            var isNull = methodCode.DeclareLocal(typeof(bool));
            var writeValue = methodCode.DeclareLocal(typeof(bool));
            var dontWrite = methodCode.DefineLabel();
            var valueType = elemType.GetGenericArguments()[0];
            var baseType = valueType;

            if (valueType.IsEnum)
            {
                baseType = valueType.GetFields()[0].FieldType;
            }

            methodCode.Emit(OpCodes.Ldloc, array);
            methodCode.Emit(OpCodes.Ldloc, i);
            methodCode.Emit(OpCodes.Ldelem, elemType);
            methodCode.Emit(OpCodes.Stloc, value);
            methodCode.Emit(OpCodes.Ldloca, value);
            methodCode.Emit(OpCodes.Call, elemType.GetProperty("HasValue").GetGetMethod());
            methodCode.Emit(OpCodes.Stloc, isNull);
            methodCode.Emit(OpCodes.Ldloc, bw);
            methodCode.Emit(OpCodes.Ldloc, isNull);
            methodCode.Emit(OpCodes.Callvirt, typeof(BinaryWriter).GetMethod("Write", new Type[] { typeof(bool) }));
            methodCode.Emit(OpCodes.Ldloc, isNull);
            methodCode.Emit(OpCodes.Ldc_I4_1);
            methodCode.Emit(OpCodes.Ceq);
            methodCode.Emit(OpCodes.Stloc, writeValue);
            methodCode.Emit(OpCodes.Ldloc, writeValue);
            methodCode.Emit(OpCodes.Brfalse_S, dontWrite);
            methodCode.Emit(OpCodes.Ldloc, bw); // binary writer
            methodCode.Emit(OpCodes.Ldloca, value);
            methodCode.Emit(OpCodes.Call, elemType.GetProperty("Value").GetGetMethod());
            if (valueType.IsEnum)
            {
                methodCode.Emit(OpCodes.Callvirt, typeof(BinaryWriter).GetMethod("Write", new Type[] { baseType }));
            }
            else
            {
                methodCode.Emit(OpCodes.Callvirt, typeof(BinaryWriter).GetMethod("Write", new Type[] { valueType }));
            }
            methodCode.MarkLabel(dontWrite);
        }

        private static void SerializeValueTypeArrayElement(ILGenerator methodCode, LocalBuilder value, LocalBuilder i, Type elemType, LocalBuilder bw)
        {
            methodCode.Emit(OpCodes.Ldloc, bw);
            methodCode.Emit(OpCodes.Ldloc, value);
            methodCode.Emit(OpCodes.Ldloc, i);
            methodCode.Emit(OpCodes.Ldelem, elemType);
            if (elemType.IsEnum)
            {
                methodCode.Emit(OpCodes.Callvirt, typeof(BinaryWriter).GetMethod("Write", new Type[] { elemType.GetFields()[0].FieldType }));
            }
            else
            {
                methodCode.Emit(OpCodes.Callvirt, typeof(BinaryWriter).GetMethod("Write", new Type[] { elemType }));
            }
        }

        private static void SerializeNullableDateTime(TypeBuilder typeBuilder, Type interfaceType, ILGenerator methodCode, PropertyInfo member, LocalBuilder bw)
        {
            var type = member.PropertyType;
            var value = methodCode.DeclareLocal(type);
            var date = methodCode.DeclareLocal(type.GetGenericArguments()[0]);
            var binaryDate = methodCode.DeclareLocal(typeof(long));
            var isNull = methodCode.DeclareLocal(typeof(bool));
            var writeValue = methodCode.DeclareLocal(typeof(bool));
            var dontWrite = methodCode.DefineLabel();

            methodCode.Emit(OpCodes.Ldarg_0); // object to read

            methodCode.Emit(OpCodes.Callvirt, ((PropertyInfo)member).GetMethod);

            methodCode.Emit(OpCodes.Stloc, value);
            methodCode.Emit(OpCodes.Ldloca, value);
            methodCode.Emit(OpCodes.Call, type.GetProperty("HasValue").GetMethod);
            methodCode.Emit(OpCodes.Stloc, isNull);
            methodCode.Emit(OpCodes.Ldloc, bw);
            methodCode.Emit(OpCodes.Ldloc, isNull);
            methodCode.Emit(OpCodes.Callvirt, typeof(BinaryWriter).GetMethod("Write", new Type[] { typeof(bool) }));
            methodCode.Emit(OpCodes.Ldloc, isNull);
            methodCode.Emit(OpCodes.Ldc_I4_1);
            methodCode.Emit(OpCodes.Ceq);
            methodCode.Emit(OpCodes.Stloc, writeValue);
            methodCode.Emit(OpCodes.Ldloc, writeValue);
            methodCode.Emit(OpCodes.Brfalse_S, dontWrite);

            methodCode.Emit(OpCodes.Ldloca, value);
            methodCode.Emit(OpCodes.Call, type.GetProperty("Value").GetMethod);
            methodCode.Emit(OpCodes.Stloc, date);
            methodCode.Emit(OpCodes.Ldloca, date);
            methodCode.Emit(OpCodes.Call, typeof(DateTime).GetMethod("ToBinary", BindingFlags.Public | BindingFlags.Instance));
            methodCode.Emit(OpCodes.Stloc, binaryDate);
            methodCode.Emit(OpCodes.Ldloc, bw); // binary writer
            methodCode.Emit(OpCodes.Ldloc, binaryDate);
            methodCode.Emit(OpCodes.Callvirt, typeof(BinaryWriter).GetMethod("Write", new Type[] { typeof(long) }));

            methodCode.MarkLabel(dontWrite);
        }

        private static void SerializeString(TypeBuilder typeBuilder, Type interfaceType, ILGenerator methodCode, PropertyInfo member, LocalBuilder bw)
        {
            /*

            IL_0016:  ldloc.0
            IL_0017:  callvirt   instance string 'Altus.Suffūz.Tests'.SimplePOCO::get_Q()
            IL_001c:  stloc.3
            IL_001d:  ldloc.3
            IL_001e:  ldnull
            IL_001f:  ceq
            IL_0021:  stloc.s    isNull
            IL_0023:  ldloc.2
            IL_0024:  ldloc.s    isNull
            IL_0026:  callvirt   instance void [mscorlib]System.IO.BinaryWriter::Write(bool)
            IL_002b:  nop
            IL_002c:  ldloc.s    isNull
            IL_002e:  ldc.i4.0
            IL_002f:  ceq
            IL_0031:  stloc.s    V_5
            IL_0033:  ldloc.s    V_5
            IL_0035:  brfalse.s  IL_003f
            IL_0037:  nop
            IL_0038:  ldloc.2
            IL_0039:  ldloc.3
            IL_003a:  callvirt   instance void [mscorlib]System.IO.BinaryWriter::Write(string)
            IL_003f:  nop

            */
            var text = methodCode.DeclareLocal(typeof(string));
            var isNull = methodCode.DeclareLocal(typeof(bool));
            var writeValue = methodCode.DeclareLocal(typeof(bool));
            var dontWrite = methodCode.DefineLabel();

            methodCode.Emit(OpCodes.Ldarg_0); // object to read
            methodCode.Emit(OpCodes.Callvirt, ((PropertyInfo)member).GetMethod);
            methodCode.Emit(OpCodes.Stloc, text);
            methodCode.Emit(OpCodes.Ldloc, text);
            methodCode.Emit(OpCodes.Ldnull);
            methodCode.Emit(OpCodes.Ceq);
            methodCode.Emit(OpCodes.Stloc, isNull);
            methodCode.Emit(OpCodes.Ldloc, bw);
            methodCode.Emit(OpCodes.Ldloc, isNull);
            methodCode.Emit(OpCodes.Callvirt, typeof(BinaryWriter).GetMethod("Write", new Type[] { typeof(bool) }));
            methodCode.Emit(OpCodes.Ldloc, isNull);
            methodCode.Emit(OpCodes.Ldc_I4_0);
            methodCode.Emit(OpCodes.Ceq);
            methodCode.Emit(OpCodes.Stloc, writeValue);
            methodCode.Emit(OpCodes.Ldloc, writeValue);
            methodCode.Emit(OpCodes.Brfalse_S, dontWrite);
            methodCode.Emit(OpCodes.Ldloc, bw); // binary writer
            methodCode.Emit(OpCodes.Ldloc, text);
            methodCode.Emit(OpCodes.Callvirt, typeof(BinaryWriter).GetMethod("Write", new Type[] { typeof(string) }));
            methodCode.MarkLabel(dontWrite);
        }

        private static void SerializeDateTime(TypeBuilder typeBuilder, Type interfaceType, ILGenerator methodCode, PropertyInfo member, LocalBuilder bw)
        {
            var binaryDate = methodCode.DeclareLocal(typeof(long));
            var date = methodCode.DeclareLocal(typeof(DateTime));

            methodCode.Emit(OpCodes.Ldarg_0); // object to read

            methodCode.Emit(OpCodes.Callvirt, ((PropertyInfo)member).GetMethod);

            methodCode.Emit(OpCodes.Stloc, date);
            methodCode.Emit(OpCodes.Ldloca, date);
            methodCode.Emit(OpCodes.Call, typeof(DateTime).GetMethod("ToBinary", BindingFlags.Public | BindingFlags.Instance));
            methodCode.Emit(OpCodes.Stloc, binaryDate);
            methodCode.Emit(OpCodes.Ldloc, bw); // binary writer
            methodCode.Emit(OpCodes.Ldloc, binaryDate);
            methodCode.Emit(OpCodes.Callvirt, typeof(BinaryWriter).GetMethod("Write", new Type[] { typeof(long) }));
        }

        private static void SerializeCharArray(TypeBuilder typeBuilder, Type interfaceType, ILGenerator methodCode, PropertyInfo member, LocalBuilder bw)
        {
            var array = methodCode.DeclareLocal(typeof(char[]));
            var arrayLength = methodCode.DeclareLocal(typeof(int));
            var isNull = methodCode.DeclareLocal(typeof(bool));
            var writeValue = methodCode.DeclareLocal(typeof(bool));
            var dontWrite = methodCode.DefineLabel();

            methodCode.Emit(OpCodes.Ldarg_0); // object to read

            methodCode.Emit(OpCodes.Callvirt, ((PropertyInfo)member).GetMethod);
            methodCode.Emit(OpCodes.Stloc, array);
            methodCode.Emit(OpCodes.Ldloc, array);
            methodCode.Emit(OpCodes.Ldnull);
            methodCode.Emit(OpCodes.Ceq);
            methodCode.Emit(OpCodes.Stloc, isNull);
            methodCode.Emit(OpCodes.Ldloc, bw);
            methodCode.Emit(OpCodes.Ldloc, isNull);
            methodCode.Emit(OpCodes.Callvirt, typeof(BinaryWriter).GetMethod("Write", new Type[] { typeof(bool) }));
            methodCode.Emit(OpCodes.Ldloc, isNull);
            methodCode.Emit(OpCodes.Ldc_I4_0);
            methodCode.Emit(OpCodes.Ceq);
            methodCode.Emit(OpCodes.Stloc, writeValue);
            methodCode.Emit(OpCodes.Ldloc, writeValue);
            methodCode.Emit(OpCodes.Brfalse_S, dontWrite);
            methodCode.Emit(OpCodes.Ldloc, array);
            methodCode.Emit(OpCodes.Callvirt, typeof(byte[]).GetProperty("Length").GetMethod);
            methodCode.Emit(OpCodes.Stloc, arrayLength);
            methodCode.Emit(OpCodes.Ldloc, bw); // binary writer
            methodCode.Emit(OpCodes.Ldloc, arrayLength);
            methodCode.Emit(OpCodes.Callvirt, typeof(BinaryWriter).GetMethod("Write", new Type[] { typeof(int) }));
            methodCode.Emit(OpCodes.Ldloc, bw); // binary writer
            methodCode.Emit(OpCodes.Ldloc, array);
            methodCode.Emit(OpCodes.Callvirt, typeof(BinaryWriter).GetMethod("Write", new Type[] { member.PropertyType }));
            methodCode.MarkLabel(dontWrite);
            methodCode.Emit(OpCodes.Nop);
        }

        private static void SerializeByteArray(TypeBuilder typeBuilder, Type interfaceType, ILGenerator methodCode, PropertyInfo member, LocalBuilder bw)
        {
            var array = methodCode.DeclareLocal(typeof(byte[]));
            var arrayLength = methodCode.DeclareLocal(typeof(int));
            var isNull = methodCode.DeclareLocal(typeof(bool));
            var writeValue = methodCode.DeclareLocal(typeof(bool));
            var dontWrite = methodCode.DefineLabel();

            methodCode.Emit(OpCodes.Ldarg_0); // object to read

            methodCode.Emit(OpCodes.Callvirt, ((PropertyInfo)member).GetMethod);

            methodCode.Emit(OpCodes.Stloc, array);
            methodCode.Emit(OpCodes.Ldloc, array);
            methodCode.Emit(OpCodes.Ldnull);
            methodCode.Emit(OpCodes.Ceq);
            methodCode.Emit(OpCodes.Stloc, isNull);
            methodCode.Emit(OpCodes.Ldloc, bw);
            methodCode.Emit(OpCodes.Ldloc, isNull);
            methodCode.Emit(OpCodes.Callvirt, typeof(BinaryWriter).GetMethod("Write", new Type[] { typeof(bool) }));
            methodCode.Emit(OpCodes.Ldloc, isNull);
            methodCode.Emit(OpCodes.Ldc_I4_0);
            methodCode.Emit(OpCodes.Ceq);
            methodCode.Emit(OpCodes.Stloc, writeValue);
            methodCode.Emit(OpCodes.Ldloc, writeValue);
            methodCode.Emit(OpCodes.Brfalse_S, dontWrite);
            methodCode.Emit(OpCodes.Ldloc, array);
            methodCode.Emit(OpCodes.Callvirt, typeof(byte[]).GetProperty("Length").GetMethod);
            methodCode.Emit(OpCodes.Stloc, arrayLength);
            methodCode.Emit(OpCodes.Ldloc, bw); // binary writer
            methodCode.Emit(OpCodes.Ldloc, arrayLength);
            methodCode.Emit(OpCodes.Callvirt, typeof(BinaryWriter).GetMethod("Write", new Type[] { typeof(int) }));
            methodCode.Emit(OpCodes.Ldloc, bw); // binary writer
            methodCode.Emit(OpCodes.Ldloc, array);
            methodCode.Emit(OpCodes.Callvirt, typeof(BinaryWriter).GetMethod("Write", new Type[] { member.PropertyType }));
            methodCode.MarkLabel(dontWrite);
            methodCode.Emit(OpCodes.Nop);
        }

        private static void SerializeNullableValueType(TypeBuilder typeBuilder, Type interfaceType, ILGenerator methodCode, PropertyInfo member, LocalBuilder bw)
        {
            /*

            IL_0016:  ldloc.0
            IL_0017:  callvirt   instance valuetype [mscorlib]System.Nullable`1<bool> 'Altus.Suffūz.Tests'.SimplePOCO::get_nA()
            IL_001c:  stloc.3
            IL_001d:  ldloca.s   'value'
            IL_001f:  call       instance bool valuetype [mscorlib]System.Nullable`1<bool>::get_HasValue()
            IL_0024:  stloc.s    isNull
            IL_0026:  ldloc.2
            IL_0027:  ldloc.s    isNull
            IL_0029:  callvirt   instance void [mscorlib]System.IO.BinaryWriter::Write(bool)
            IL_002e:  nop
            IL_002f:  ldloc.s    isNull
            IL_0031:  ldc.i4.0
            IL_0032:  ceq
            IL_0034:  stloc.s    V_5
            IL_0036:  ldloc.s    V_5
            IL_0038:  brfalse.s  IL_004a
            IL_003a:  nop
            IL_003b:  ldloc.2
            IL_003c:  ldloca.s   'value'
            IL_003e:  call       instance !0 valuetype [mscorlib]System.Nullable`1<bool>::get_Value()
            IL_0043:  callvirt   instance void [mscorlib]System.IO.BinaryWriter::Write(bool)
            IL_0048:  nop

            */
            var type = member.PropertyType;
            var value = methodCode.DeclareLocal(type);
            var isNull = methodCode.DeclareLocal(typeof(bool));
            var writeValue = methodCode.DeclareLocal(typeof(bool));
            var dontWrite = methodCode.DefineLabel();
            var valueType = type.GetGenericArguments()[0];
            var baseType = valueType;

            if (valueType.IsEnum)
            {
                baseType = valueType.GetFields()[0].FieldType;
            }

            methodCode.Emit(OpCodes.Ldarg_0); // object to read

            methodCode.Emit(OpCodes.Callvirt, ((PropertyInfo)member).GetMethod);

            methodCode.Emit(OpCodes.Stloc, value);
            methodCode.Emit(OpCodes.Ldloca, value);
            methodCode.Emit(OpCodes.Call, type.GetProperty("HasValue").GetMethod);
            methodCode.Emit(OpCodes.Stloc, isNull);
            methodCode.Emit(OpCodes.Ldloc, bw);
            methodCode.Emit(OpCodes.Ldloc, isNull);
            methodCode.Emit(OpCodes.Callvirt, typeof(BinaryWriter).GetMethod("Write", new Type[] { typeof(bool) }));
            methodCode.Emit(OpCodes.Ldloc, isNull);
            methodCode.Emit(OpCodes.Ldc_I4_1);
            methodCode.Emit(OpCodes.Ceq);
            methodCode.Emit(OpCodes.Stloc, writeValue);
            methodCode.Emit(OpCodes.Ldloc, writeValue);
            methodCode.Emit(OpCodes.Brfalse_S, dontWrite);
            methodCode.Emit(OpCodes.Ldloc, bw); // binary writer
            methodCode.Emit(OpCodes.Ldloca, value);
            methodCode.Emit(OpCodes.Call, type.GetProperty("Value").GetMethod);
            if (valueType.IsEnum)
            {
                methodCode.Emit(OpCodes.Callvirt, typeof(BinaryWriter).GetMethod("Write", new Type[] { baseType }));
            }
            else
            {
                methodCode.Emit(OpCodes.Callvirt, typeof(BinaryWriter).GetMethod("Write", new Type[] { valueType }));
            }
            methodCode.MarkLabel(dontWrite);
            methodCode.Emit(OpCodes.Nop);
        }

        private static void SerializeValueType(TypeBuilder typeBuilder, Type interfaceType, ILGenerator methodCode, PropertyInfo member, LocalBuilder bw)
        {
            var type = member.PropertyType;
            methodCode.Emit(OpCodes.Ldloc, bw); // binary writer
            methodCode.Emit(OpCodes.Ldarg_0); // object to read

            methodCode.Emit(OpCodes.Callvirt, ((PropertyInfo)member).GetMethod);

            if (type.IsEnum)
            {
                methodCode.Emit(OpCodes.Callvirt, typeof(BinaryWriter).GetMethod("Write", new Type[] { type.GetEnumUnderlyingType() }));
            }
            else
            {
                methodCode.Emit(OpCodes.Callvirt, typeof(BinaryWriter).GetMethod("Write", new Type[] { type }));
            }
        }


        private static bool IsValueType(PropertyInfo member)
        {
            var memberType = member.PropertyType;
            return IsValueType(memberType);
        }

        private static bool IsValueType(Type type)
        {
            return type == typeof(bool)
                || type == typeof(byte)
                || type == typeof(sbyte)
                || type == typeof(char)
                || type == typeof(short)
                || type == typeof(ushort)
                || type == typeof(int)
                || type == typeof(uint)
                || type == typeof(long)
                || type == typeof(ulong)
                || type == typeof(float)
                || type == typeof(double)
                || type == typeof(decimal)
                || type.IsEnum
                ;
        }

        private static bool IsNullableValueType(PropertyInfo member)
        {
            return IsNullableValueType(member.PropertyType);
        }
        private static bool IsNullableValueType(Type type)
        {
            return type == typeof(bool?)
                || type == typeof(byte?)
                || type == typeof(sbyte?)
                || type == typeof(char?)
                || type == typeof(short?)
                || type == typeof(ushort?)
                || type == typeof(int?)
                || type == typeof(uint?)
                || type == typeof(long?)
                || type == typeof(ulong?)
                || type == typeof(float?)
                || type == typeof(double?)
                || type == typeof(decimal?)
                || (type.IsGenericType && type.Implements(typeof(Nullable<>)) && type.GetGenericArguments()[0].IsEnum)
                ;
        }

        private static List<PropertyInfo> GetSerializableMembers(Type type)
        {
            return type.GetPublicProperties()
                       .Where(pi => pi.CanRead && pi.CanWrite && pi.GetCustomAttribute<Altus.Suffūz.Serialization.Binary.BinarySerializableAttribute>() != null)
                       .ToList();
        }

        private static FieldInfo BuildPropertyChangingEvent(Type interfaceType, TypeBuilder typeBuilder, Type baseType)
        {
            if (baseType != null && interfaceType.FindInterfaces(
                    (type, filter) => type.FullName.Contains(filter.ToString()),
                    typeof(INotifyPropertyChanging).FullName).Any())
            {
                // base type already implements it, so do nothing
                var checkType = baseType;
                FieldInfo pcFld = null;
                do
                {
                    // walk the type chain looking for the event field on the appropriate type
                    pcFld = checkType.GetField("PropertyChanging", BindingFlags.NonPublic | BindingFlags.Instance);
                    checkType = checkType.BaseType;
                } while (pcFld == null && !checkType.Equals(typeof(Object)) && !checkType.BaseType.Equals(typeof(Object)));
                if (pcFld != null)
                    return pcFld;
            }


            var eventField = typeBuilder.DefineField("PropertyChanging",
                typeof(PropertyChangingEventHandler),
                FieldAttributes.Private);
            var eventBuilder = typeBuilder.DefineEvent("PropertyChanging",
                EventAttributes.None,
                typeof(PropertyChangingEventHandler));

            eventBuilder.SetAddOnMethod(
            CreateAddRemoveChangingMethod(typeBuilder, eventField, true));
            eventBuilder.SetRemoveOnMethod(
            CreateAddRemoveChangingMethod(typeBuilder, eventField, false));

            return eventField;
        }

        protected static MethodInfo BuildGetKeyMethod(Type interfaceType, TypeBuilder typeBuilder, PropertyInfo keyProp)
        {
            var getKey = typeBuilder.DefineMethod("GetKey",
                MethodAttributes.Public
                | MethodAttributes.HideBySig
                | MethodAttributes.NewSlot
                | MethodAttributes.Virtual
                | MethodAttributes.Final,
                CallingConventions.Standard,
                typeof(string),
                Type.EmptyTypes);

            var getKeyCode = getKey.GetILGenerator();
            
            if (keyProp == null)
            {
                getKeyCode.Emit(OpCodes.Ldnull);
            }
            else
            {
                var local = getKeyCode.DeclareLocal(keyProp.PropertyType);
                getKeyCode.Emit(OpCodes.Ldarg_0);
                getKeyCode.Emit(OpCodes.Call, keyProp.GetMethod);
                getKeyCode.Emit(OpCodes.Stloc, local);
                getKeyCode.Emit(OpCodes.Ldloca_S, local);
                if (keyProp.PropertyType != typeof(string))
                    getKeyCode.Emit(OpCodes.Call, keyProp.PropertyType.GetMethod("ToString", Type.EmptyTypes));
            }
            getKeyCode.Emit(OpCodes.Ret);

            typeBuilder.DefineMethodOverride(getKey, typeof(IModel).GetMethod("GetKey"));
            return getKey;
        }

        protected static MethodInfo BuildSetKeyMethod(Type interfaceType, TypeBuilder typeBuilder, PropertyInfo keyProp)
        {
            var setKey = typeBuilder.DefineMethod("SetKey",
                MethodAttributes.Public
                | MethodAttributes.HideBySig
                | MethodAttributes.NewSlot
                | MethodAttributes.Virtual
                | MethodAttributes.Final,
                CallingConventions.Standard,
                typeof(void),
                new Type[] { typeof(string) });

            var setKeyCode = setKey.GetILGenerator();
            if (keyProp != null)
            {
                setKeyCode.Emit(OpCodes.Ldarg_0);
                setKeyCode.Emit(OpCodes.Ldarg_1);
                if (keyProp.PropertyType != typeof(string))
                    setKeyCode.Emit(OpCodes.Call, keyProp.PropertyType.GetMethod("Parse", BindingFlags.Public | BindingFlags.Static, null, new Type[] { typeof(string) }, null));
                setKeyCode.Emit(OpCodes.Call, keyProp.SetMethod);
            }
            setKeyCode.Emit(OpCodes.Ret);

            typeBuilder.DefineMethodOverride(setKey, typeof(IModel).GetMethod("SetKey"));
            return setKey;
        }

        public static Func<Dictionary<string, object>, T> CreateModelInstanceActivator<T>(Type baseType = null, bool loadFromCache = true, params Type[] additionalInterfaces) where T : IModel
        {
            var type = CreateModelType<T>(baseType, loadFromCache, additionalInterfaces);
            return (dict) =>
            {
                if (dict == null)
                    return (T)Activator.CreateInstance(type);
                else
                    return (T)Activator.CreateInstance(type, dict);
            };
        }

        public static Func<Dictionary<string, object>, object> CreateModelInstanceActivator(Type interfaceType, Type baseType = null, bool loadFromCache = true, params Type[] additionalInterfaces)
        {
            var type = CreateModelType(interfaceType, baseType, loadFromCache, additionalInterfaces);
            return (dict) =>
            {
                if (dict == null)
                    return Activator.CreateInstance(type);
                else
                    return Activator.CreateInstance(type, dict);
            };
        }

        public static T CreateModelInstance<T>(Type baseType = null, bool loadFromCache = true, params Type[] additionalInterfaces)
        {
            return (T)CreateModelInstance(typeof(T), baseType, loadFromCache, additionalInterfaces);
        }

        public static object CreateModelInstance(Type interfaceType, Type baseType = null, bool loadFromCache = true, params Type[] additionalInterfaces)
        {
            return Activator.CreateInstance(CreateModelType(interfaceType, baseType, loadFromCache, additionalInterfaces));
        }

        public static Func<IModel, IRepository, IModel> CreateTrackedModelInstanceActivator(Type interfaceType, bool loadFromCache = true)
        {
            var type = CreateTrackedModelType(interfaceType, loadFromCache);
            return (model, repository) =>
            {
                return (IModel)Activator.CreateInstance(type, new Object[] { model, repository });
            };
        }

        public static Func<T, IRepository, T> CreateTrackedModelInstanceActivator<T>( bool loadFromCache = true) where T : IModel
        {
            var type = CreateTrackedModelType<T>(loadFromCache);
            return (model, repository) =>
            {
                return (T)Activator.CreateInstance(type, new Object[] { model, repository });
            };
        }

        public static Type CreateTrackedModelType<T>(bool loadFromCache = true)
        {
            return CreateTrackedModelType(typeof(T), loadFromCache);
        }

        public static Type CreateTrackedModelType(Type interfaceType, bool loadFromCache = true)
        {
            if (!interfaceType.IsInterface) throw new InvalidOperationException("The generic parameter type T must be an interface.");
            if (!interfaceType.Implements<IModel>()) throw new InvalidOperationException("The generic parameter type T must derive from IModel.");

            Type instanceType = null;
            var className = "Dyn_Tracked_" + interfaceType.FullName.Replace(".", "_");
            lock (_buildCache)
            {
                if (!loadFromCache || !_buildCache.TryGetValue(className, out instanceType))
                {
                    var baseType = ModelTypeConverter.TrackedModelBaseType;

                    if (!baseType.Implements<IBinarySerializable>())
                        throw new InvalidOperationException("The Tracked Model base type must implement the IBinarySerializable interface");
                    if (!baseType.Implements<IProxyModel>())
                        throw new InvalidOperationException("The Tracked Model base type must implement the IProxyModel interface");

                    var typeBuilder = _modBuilder.DefineType(className,
                        TypeAttributes.Public
                        | TypeAttributes.AutoClass
                        | TypeAttributes.AnsiClass
                        | TypeAttributes.Class
                        | TypeAttributes.Serializable
                        | TypeAttributes.BeforeFieldInit);

                    typeBuilder.AddInterfaceImplementation(interfaceType);
                    typeBuilder.AddInterfaceImplementation(typeof(ITrackedModel));
                    typeBuilder.AddInterfaceImplementation(typeof(IAny));

                    typeBuilder.SetParent(baseType);

                    var members = GetMembers(interfaceType)
                                    .OfType<PropertyInfo>()
                                    .Distinct(new PropertyInfoComparer())
                                    .ToArray();
                    var baseMembers = baseType.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                                              .Distinct(new PropertyInfoComparer()).ToArray();

                    var props = new List<PropertyInfo>();

                    foreach (var member in members)//.Where(mi => !baseMembers.Any(pi => pi.Name.Equals(mi.Name) && pi.PropertyType.Equals(((PropertyInfo)mi).PropertyType))))
                    {
                        if (member is PropertyInfo)
                        {
                            if (!member.Name.Equals("ModelType") && !member.Name.Equals("InstanceName"))
                            {
                                props.Add(BuildProxyProperty(interfaceType,
                                    typeBuilder,
                                    baseType,
                                    (PropertyInfo)member));
                            }
                        }
                    }
                    var trackingKeyProp = BuildTrackingKeyProperty(typeBuilder);
                    BuildCtor(interfaceType, typeBuilder, trackingKeyProp, props.Single(pi => pi.Name.Equals("Created")), props.Single(pi => pi.Name.Equals("Modified")));
                    BuildGetKeyMethod(interfaceType, typeBuilder, props.SingleOrDefault(p => p.Name.Equals("Key")));
                    BuildSetKeyMethod(interfaceType, typeBuilder, props.SingleOrDefault(p => p.Name.Equals("Key")));
                    BuildCloneEnumerableIModelProperties(interfaceType, typeBuilder, props.Where(p =>
                                        (p.PropertyType.Implements(typeof(IEnumerable<>))
                                         && p.PropertyType.GetGenericArguments().Length == 1
                                         && p.PropertyType.GetGenericArguments()[0].Implements<IModel>())
                                        ||
                                        (p.PropertyType.IsArray && p.PropertyType.GetElementType().Implements<IModel>())));
                    BuildRaiseModelPropertyChangedEvents(interfaceType, typeBuilder, props.Where(p => p.CanWrite));
                    BuildCloneIModelProperties(interfaceType, typeBuilder, props.Where(p => p.PropertyType.Implements<IModel>()));
                    BuildTrackedCompareMethod(interfaceType, typeBuilder, baseType, typeof(ITrackedModel).GetProperty("Current"));


                    instanceType = typeBuilder.CreateType();
                    if (_buildCache.ContainsKey(className))
                    {
                        _buildCache[className] = instanceType;
                    }
                    else
                    {
                        _buildCache.Add(className, instanceType);
                    }
                }
                else
                {
                    instanceType = _buildCache[className];
                }
            }
            return instanceType;
        }

        private static MethodInfo BuildTrackedCompareMethod(Type interfaceType, TypeBuilder typeBuilder, Type baseType, PropertyInfo current)
        {
            /* IL_0001:  ldarg.0
  IL_0002:  call       instance class [Data.Core]Data.Core.IModel UX.Core.Data.TrackedModelBase::get_Current()
  IL_0007:  ldarg.1
  IL_0008:  ldarg.2
  IL_0009:  callvirt   instance class [mscorlib]System.Collections.Generic.IEnumerable`1<class [Data.Core]Data.Core.Auditing.AuditedChange> [Data.Core]Data.Core.IModel::Compare(class [Data.Core]Data.Core.IModel,
                                                                                                                                                                                                 string)
  IL_000e:  stloc.0
  IL_000f:  br.s       IL_0011
  IL_0011:  ldloc.0
  IL_0012:  ret
 */

            var compare = typeBuilder.DefineMethod("Compare",
                MethodAttributes.Public
                | MethodAttributes.HideBySig
                | MethodAttributes.NewSlot
                | MethodAttributes.Virtual
                | MethodAttributes.Final,
                CallingConventions.Standard,
                typeof(IEnumerable<AuditedChange>),
                new Type[] { typeof(IModel), typeof(string) });

            var code = compare.GetILGenerator();

            code.Emit(OpCodes.Ldarg_0);
            code.Emit(OpCodes.Call, current.GetMethod);
            code.Emit(OpCodes.Ldarg_1);
            code.Emit(OpCodes.Ldarg_2);
            code.Emit(OpCodes.Callvirt, typeof(IModel).GetMethod("Compare"));
            code.Emit(OpCodes.Ret);

            typeBuilder.DefineMethodOverride(compare, typeof(IModel).GetMethod("Compare"));
            return compare;
        }

        private static PropertyInfo BuildTrackingKeyProperty(TypeBuilder typeBuilder)
        {
            var member = typeof(ITrackedModel).GetProperty("TrackingKey");

            var property = typeBuilder.DefineProperty(member.Name,
                PropertyAttributes.HasDefault,
                member.PropertyType,
                null);
            var fld = typeBuilder.DefineField("_" + member.Name, member.PropertyType, FieldAttributes.Private);

            if (member.CanRead)
            {
                var getter = typeBuilder.DefineMethod("get_" + member.Name,
                MethodAttributes.Public
                | MethodAttributes.SpecialName
                | MethodAttributes.HideBySig
                | MethodAttributes.NewSlot
                | MethodAttributes.Final
                | MethodAttributes.Virtual,
                member.PropertyType,
                Type.EmptyTypes);
                var getterCode = getter.GetILGenerator();

                getterCode.Emit(OpCodes.Ldarg_0);
                getterCode.Emit(OpCodes.Ldfld, fld);
                getterCode.Emit(OpCodes.Ret);

                property.SetGetMethod(getter);
                typeBuilder.DefineMethodOverride(getter, member.GetMethod);
            }

            if (member.CanWrite)
            {
                var setter = typeBuilder.DefineMethod("set_" + member.Name,
                    MethodAttributes.Public
                    | MethodAttributes.SpecialName
                    | MethodAttributes.HideBySig
                    | MethodAttributes.NewSlot
                    | MethodAttributes.Final
                    | MethodAttributes.Virtual,
                    null,
                    new[] { member.PropertyType });
                var setterCode = setter.GetILGenerator();

                setterCode.Emit(OpCodes.Ldarg_0);
                setterCode.Emit(OpCodes.Ldarg_1);
                setterCode.Emit(OpCodes.Stfld, fld);
                setterCode.Emit(OpCodes.Ret);

                property.SetSetMethod(setter);
                typeBuilder.DefineMethodOverride(setter, member.SetMethod);
            }

            return property;
        }

        private static MethodInfo BuildCloneIModelProperties(Type interfaceType, TypeBuilder typeBuilder, IEnumerable<PropertyInfo> enumerable)
        {
            var method = typeBuilder.DefineMethod("CloneIModelProperties",
                MethodAttributes.HideBySig
                | MethodAttributes.Family
                | MethodAttributes.Virtual,
                CallingConventions.Standard,
                typeof(void),
                new Type[] { typeof(IModel), typeof(IModel) });

            var code = method.GetILGenerator();
            var source = code.DeclareLocal(interfaceType);
            var dest = code.DeclareLocal(interfaceType);
            var cloneMethod = typeBuilder.BaseType.GetMethod("CloneDynamicItems", BindingFlags.NonPublic | BindingFlags.Instance);

            code.Emit(OpCodes.Ldarg_1);
            code.Emit(OpCodes.Castclass, interfaceType);
            code.Emit(OpCodes.Stloc, source.LocalIndex);

            code.Emit(OpCodes.Ldarg_2);
            code.Emit(OpCodes.Castclass, interfaceType);
            code.Emit(OpCodes.Stloc, dest.LocalIndex);

            foreach (var prop in enumerable)
            {
                var iProp = interfaceType.GetPublicProperty(prop.Name);
                if (iProp != null)
                {
                    code.Emit(OpCodes.Ldarg_0);
                    code.Emit(OpCodes.Ldloc, source.LocalIndex);
                    code.Emit(OpCodes.Callvirt, iProp.GetMethod);
                    code.Emit(OpCodes.Ldloc, dest.LocalIndex);
                    code.Emit(OpCodes.Callvirt, iProp.GetMethod);
                    code.Emit(OpCodes.Callvirt, cloneMethod);
                }
            }

            code.Emit(OpCodes.Ret);

            typeBuilder.DefineMethodOverride(method, typeBuilder.BaseType.GetMethod("CloneIModelProperties", BindingFlags.NonPublic | BindingFlags.Instance));
            return method;
        }

        private static MethodInfo BuildRaiseModelPropertyChangedEvents(Type interfaceType, TypeBuilder typeBuilder, IEnumerable<PropertyInfo> props)
        {
            var method = typeBuilder.DefineMethod("RaiseModelPropertyChangedEvents",
               MethodAttributes.HideBySig
               | MethodAttributes.Family
               | MethodAttributes.Virtual,
               CallingConventions.Standard,
               typeof(void),
               Type.EmptyTypes);

            var code = method.GetILGenerator();
            var propChanged = typeBuilder.BaseType.GetMethod("OnPropertyChanged", BindingFlags.Instance | BindingFlags.NonPublic, null, new Type[] { typeof(string) }, null);

            foreach (var prop in props)
            {
                code.Emit(OpCodes.Ldarg_0);
                code.Emit(OpCodes.Ldstr, prop.Name);
                code.Emit(OpCodes.Callvirt, propChanged);
            }

            code.Emit(OpCodes.Ret);

            typeBuilder.DefineMethodOverride(method, typeBuilder.BaseType.GetMethod("RaiseModelPropertyChangedEvents", BindingFlags.NonPublic | BindingFlags.Instance));
            return method;
        }

        private static PropertyInfo BuildProxyProperty(Type interfaceType, TypeBuilder typeBuilder, Type baseType, PropertyInfo member)
        {
            var currentProp = typeBuilder.BaseType.GetProperty("Current");

            var property = typeBuilder.DefineProperty(member.Name,
                PropertyAttributes.HasDefault,
                member.PropertyType,
                null);

            var castType = baseType.GetPublicProperties().Where(pi => pi.Name.Equals(member.Name) && pi.PropertyType.Equals(member.PropertyType))
                                                         .Select(pi => pi.DeclaringType)
                                                         .FirstOrDefault();
            if (castType == null)
            {
                castType = member.DeclaringType;
            }

            if (member.CanRead)
            {
                var getter = typeBuilder.DefineMethod("get_" + member.Name,
                MethodAttributes.Public
                | MethodAttributes.SpecialName
                | MethodAttributes.HideBySig
                | MethodAttributes.NewSlot
                | MethodAttributes.Final
                | MethodAttributes.Virtual,
                member.PropertyType,
                Type.EmptyTypes);
                var getterCode = getter.GetILGenerator();

                getterCode.Emit(OpCodes.Ldarg_0);
                if (castType != baseType)
                {
                    getterCode.Emit(OpCodes.Call, currentProp.GetMethod);
                    getterCode.Emit(OpCodes.Castclass, castType);
                }
                getterCode.Emit(OpCodes.Callvirt, member.GetMethod);
                getterCode.Emit(OpCodes.Ret);

                property.SetGetMethod(getter);
                typeBuilder.DefineMethodOverride(getter, member.GetMethod);
            }

            if (member.CanWrite)
            {
                var setter = typeBuilder.DefineMethod("set_" + member.Name,
                    MethodAttributes.Public
                    | MethodAttributes.SpecialName
                    | MethodAttributes.HideBySig
                    | MethodAttributes.NewSlot
                    | MethodAttributes.Final
                    | MethodAttributes.Virtual,
                    null,
                    new[] { member.PropertyType });
                var setterCode = setter.GetILGenerator();

                setterCode.Emit(OpCodes.Ldarg_0);
                if (castType != baseType)
                {
                    setterCode.Emit(OpCodes.Call, currentProp.GetMethod);
                    setterCode.Emit(OpCodes.Castclass, castType);
                }
                setterCode.Emit(OpCodes.Ldarg_1);
                setterCode.Emit(OpCodes.Callvirt, member.SetMethod);

                setterCode.Emit(OpCodes.Ret);
                property.SetSetMethod(setter);
            }

            return property;
        }

        private static MethodInfo BuildCloneEnumerableIModelProperties(Type interfaceType, TypeBuilder typeBuilder, IEnumerable<PropertyInfo> enumerable)
        {
            var method = typeBuilder.DefineMethod("CloneEnumerableIModelProperties",
                MethodAttributes.HideBySig
                | MethodAttributes.Family
                | MethodAttributes.Virtual,
                CallingConventions.Standard,
                typeof(void),
                new Type[] { typeof(IModel), typeof(IModel) });

            var cloneMethod = typeBuilder.BaseType.GetMethod("CloneDynamicItems", BindingFlags.NonPublic | BindingFlags.Instance);

            var code = method.GetILGenerator();
            var source = code.DeclareLocal(interfaceType);
            var dest = code.DeclareLocal(interfaceType);

            code.Emit(OpCodes.Ldarg_1);
            code.Emit(OpCodes.Castclass, interfaceType);
            code.Emit(OpCodes.Stloc, source.LocalIndex);

            code.Emit(OpCodes.Ldarg_2);
            code.Emit(OpCodes.Castclass, interfaceType);
            code.Emit(OpCodes.Stloc, dest.LocalIndex);

            foreach (var prop in enumerable)
            {
                var iProp = interfaceType.GetPublicProperties().Single(pi => pi.Name.Equals(prop.Name));
                var elementType = prop.PropertyType.ResolveElementType();
                var sourceArray = code.DeclareLocal(elementType.MakeArrayType());
                var destArray = code.DeclareLocal(elementType.MakeArrayType());
                var index = code.DeclareLocal(typeof(int));

                code.Emit(OpCodes.Ldloc, source.LocalIndex);
                code.Emit(OpCodes.Callvirt, iProp.GetMethod);
                code.Emit(OpCodes.Call, typeof(Enumerable).GetMethod("ToArray", BindingFlags.Public | BindingFlags.Static).MakeGenericMethod(elementType));
                code.Emit(OpCodes.Stloc, sourceArray.LocalIndex);

                code.Emit(OpCodes.Ldloc, dest.LocalIndex);
                code.Emit(OpCodes.Callvirt, iProp.GetMethod);
                code.Emit(OpCodes.Call, typeof(Enumerable).GetMethod("ToArray", BindingFlags.Public | BindingFlags.Static).MakeGenericMethod(elementType));
                code.Emit(OpCodes.Stloc, destArray.LocalIndex);

                code.Emit(OpCodes.Ldc_I4_0);
                code.Emit(OpCodes.Stloc, index.LocalIndex);

                var logicTest = code.DefineLabel();
                code.Emit(OpCodes.Br_S, logicTest);

                var loopStart = code.DefineLabel();
                code.MarkLabel(loopStart);
                code.Emit(OpCodes.Ldarg_0);
                code.Emit(OpCodes.Ldloc, sourceArray.LocalIndex);
                code.Emit(OpCodes.Ldloc, index.LocalIndex);
                code.Emit(OpCodes.Ldelem_Ref);
                code.Emit(OpCodes.Ldloc, destArray.LocalIndex);
                code.Emit(OpCodes.Ldloc, index.LocalIndex);
                code.Emit(OpCodes.Ldelem_Ref);
                code.Emit(OpCodes.Call, cloneMethod);

                code.Emit(OpCodes.Ldloc, index.LocalIndex);
                code.Emit(OpCodes.Ldc_I4_1);
                code.Emit(OpCodes.Add);
                code.Emit(OpCodes.Stloc, index.LocalIndex);
                code.MarkLabel(logicTest);
                code.Emit(OpCodes.Ldloc, index.LocalIndex);
                code.Emit(OpCodes.Ldloc, sourceArray.LocalIndex);
                code.Emit(OpCodes.Ldlen);
                code.Emit(OpCodes.Conv_I4);
                code.Emit(OpCodes.Clt);
                code.Emit(OpCodes.Brtrue_S, loopStart);
            }

            code.Emit(OpCodes.Ret);

            typeBuilder.DefineMethodOverride(method, typeBuilder.BaseType.GetMethod("CloneEnumerableIModelProperties", BindingFlags.NonPublic | BindingFlags.Instance));
            return method;
        }

        protected static ConstructorInfo BuildCtor(Type interfaceType, TypeBuilder typeBuilder, PropertyInfo trackingKeyProp, PropertyInfo created, PropertyInfo modified)
        {
            var ctor = typeBuilder.DefineConstructor(MethodAttributes.Public
                | MethodAttributes.HideBySig
                | MethodAttributes.SpecialName
                | MethodAttributes.RTSpecialName,
                CallingConventions.Standard,
                new Type[] { typeof(IModel), typeof(IRepository) });

            var code = ctor.GetILGenerator();
            var guid = code.DeclareLocal(typeof(Guid));

            code.Emit(OpCodes.Ldarg_0);
            code.Emit(OpCodes.Ldarg_1);
            code.Emit(OpCodes.Ldarg_2);
            code.Emit(OpCodes.Call, typeBuilder.BaseType.GetConstructor(BindingFlags.Instance | BindingFlags.NonPublic, null, new Type[] { typeof(IModel), typeof(IRepository) }, null));
            code.Emit(OpCodes.Ldarg_0);
            code.Emit(OpCodes.Call, typeof(Guid).GetMethod("NewGuid"));
            code.Emit(OpCodes.Stloc, guid);
            code.Emit(OpCodes.Ldloca, guid);
            code.Emit(OpCodes.Constrained, typeof(Guid));
            code.Emit(OpCodes.Callvirt, typeof(object).GetMethod("ToString", Type.EmptyTypes));
            code.Emit(OpCodes.Call, trackingKeyProp.SetMethod);
            //code.Emit(OpCodes.Ldarg_0);
            //code.Emit(OpCodes.Call, typeof(DateTime).GetProperty("Now", BindingFlags.Public | BindingFlags.Static).GetMethod);
            //code.Emit(OpCodes.Call, created.SetMethod);
            //code.Emit(OpCodes.Ldarg_0);
            //code.Emit(OpCodes.Call, typeof(DateTime).GetProperty("Now", BindingFlags.Public | BindingFlags.Static).GetMethod);
            //code.Emit(OpCodes.Call, modified.SetMethod);

            code.Emit(OpCodes.Ret);

            return ctor;
        }

        protected static FieldInfo BuildPropertyChangedEvent(Type interfaceType, TypeBuilder typeBuilder, Type baseType)
        {
            if (baseType != null && interfaceType.FindInterfaces(
                    (type, filter) => type.FullName.Contains(filter.ToString()),
                    typeof(INotifyPropertyChanged).FullName).Any())
            {
                // base type already implements it, so do nothing
                var checkType = baseType;
                FieldInfo pcFld = null;
                do
                {
                    // walk the type chain looking for the event field on the appropriate type
                    pcFld = checkType.GetField("PropertyChanged", BindingFlags.NonPublic | BindingFlags.Instance);
                    checkType = checkType.BaseType;
                } while (pcFld == null && !checkType.Equals(typeof(Object)) && !checkType.BaseType.Equals(typeof(Object)));
                if (pcFld != null)
                    return pcFld;
            }


            var eventField = typeBuilder.DefineField("PropertyChanged",
                typeof(PropertyChangedEventHandler),
                FieldAttributes.Private);
            var eventBuilder = typeBuilder.DefineEvent("PropertyChanged",
                EventAttributes.None,
                typeof(PropertyChangedEventHandler));

            eventBuilder.SetAddOnMethod(
            CreateAddRemoveChangedMethod(typeBuilder, eventField, true));
            eventBuilder.SetRemoveOnMethod(
            CreateAddRemoveChangedMethod(typeBuilder, eventField, false));

            return eventField;

        }

        protected static MethodBuilder CreateAddRemoveChangedMethod(
           TypeBuilder typeBuilder, FieldBuilder eventField, bool isAdd)
        {
            string prefix = "remove_";
            string delegateAction = "Remove";
            if (isAdd)
            {
                prefix = "add_";
                delegateAction = "Combine";
            }
            MethodBuilder addremoveMethod =
            typeBuilder.DefineMethod(prefix + "PropertyChanged",
               MethodAttributes.Public |
               MethodAttributes.SpecialName |
               MethodAttributes.NewSlot |
               MethodAttributes.HideBySig |
               MethodAttributes.Virtual |
               MethodAttributes.Final,
               null,
               new[] { typeof(PropertyChangedEventHandler) });
            MethodImplAttributes eventMethodFlags =
                MethodImplAttributes.Managed |
                MethodImplAttributes.Synchronized;
            addremoveMethod.SetImplementationFlags(eventMethodFlags);

            ILGenerator ilGen = addremoveMethod.GetILGenerator();

            // PropertyChanged += value; // PropertyChanged -= value;
            ilGen.Emit(OpCodes.Ldarg_0);
            ilGen.Emit(OpCodes.Ldarg_0);
            ilGen.Emit(OpCodes.Ldfld, eventField);
            ilGen.Emit(OpCodes.Ldarg_1);
            ilGen.EmitCall(OpCodes.Call,
                typeof(Delegate).GetMethod(
                delegateAction,
                new[] { typeof(Delegate), typeof(Delegate) }),
                null);
            ilGen.Emit(OpCodes.Castclass, typeof(
            PropertyChangedEventHandler));
            ilGen.Emit(OpCodes.Stfld, eventField);
            ilGen.Emit(OpCodes.Ret);

            MethodInfo intAddRemoveMethod =
            typeof(INotifyPropertyChanged).GetMethod(
            prefix + "PropertyChanged");
            typeBuilder.DefineMethodOverride(
            addremoveMethod, intAddRemoveMethod);

            return addremoveMethod;
        }

        protected static MethodBuilder CreateAddRemoveChangingMethod(
           TypeBuilder typeBuilder, FieldBuilder eventField, bool isAdd)
        {
            string prefix = "remove_";
            string delegateAction = "Remove";
            if (isAdd)
            {
                prefix = "add_";
                delegateAction = "Combine";
            }
            MethodBuilder addremoveMethod =
            typeBuilder.DefineMethod(prefix + "PropertyChanging",
               MethodAttributes.Public |
               MethodAttributes.SpecialName |
               MethodAttributes.NewSlot |
               MethodAttributes.HideBySig |
               MethodAttributes.Virtual |
               MethodAttributes.Final,
               null,
               new[] { typeof(PropertyChangingEventHandler) });
            MethodImplAttributes eventMethodFlags =
                MethodImplAttributes.Managed |
                MethodImplAttributes.Synchronized;
            addremoveMethod.SetImplementationFlags(eventMethodFlags);

            ILGenerator ilGen = addremoveMethod.GetILGenerator();

            // PropertyChanged += value; // PropertyChanged -= value;
            ilGen.Emit(OpCodes.Ldarg_0);
            ilGen.Emit(OpCodes.Ldarg_0);
            ilGen.Emit(OpCodes.Ldfld, eventField);
            ilGen.Emit(OpCodes.Ldarg_1);
            ilGen.EmitCall(OpCodes.Call,
                typeof(Delegate).GetMethod(
                delegateAction,
                new[] { typeof(Delegate), typeof(Delegate) }),
                null);
            ilGen.Emit(OpCodes.Castclass, typeof(
            PropertyChangingEventHandler));
            ilGen.Emit(OpCodes.Stfld, eventField);
            ilGen.Emit(OpCodes.Ret);

            MethodInfo intAddRemoveMethod =
            typeof(INotifyPropertyChanging).GetMethod(
            prefix + "PropertyChanging");
            typeBuilder.DefineMethodOverride(
            addremoveMethod, intAddRemoveMethod);

            return addremoveMethod;
        }

        protected static ConstructorInfo BuildDefaultCtor(Type instanceType, TypeBuilder typeBuilder, Type baseType, FieldInfo[] iListProps, FieldInfo propertyChanging, FieldInfo protocolBufferField)
        {
            var setPropertyChanging = BuildSetPropertyChanging(instanceType, typeBuilder);
            var setPropertyChanged = BuildSetPropertyChanged(instanceType, typeBuilder);

            var ctor = typeBuilder.DefineConstructor(MethodAttributes.Public
                | MethodAttributes.HideBySig
                | MethodAttributes.SpecialName
                | MethodAttributes.RTSpecialName,
                CallingConventions.Standard,
                Type.EmptyTypes);

            var code = ctor.GetILGenerator();
            // call base ctor
            code.Emit(OpCodes.Ldarg_0);
            code.Emit(OpCodes.Call, typeBuilder.BaseType.GetConstructor(BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic, null, new Type[0], null));
            code.Emit(OpCodes.Ldarg_0);
            code.Emit(OpCodes.Ldc_I4_0);
            code.Emit(OpCodes.Newarr, typeof(byte));
            code.Emit(OpCodes.Stfld, protocolBufferField);

            foreach (var fld in iListProps)
            {
                if (fld.FieldType.Implements<INotifyCollectionChanging>())
                {
                    // _otherAddresses = new Flock<IAddress>();
                    code.Emit(OpCodes.Ldarg_0);
                    code.Emit(OpCodes.Newobj, fld.FieldType.GetConstructor(Type.EmptyTypes));
                    code.Emit(OpCodes.Stfld, fld);
                    // _otherAddresses.CollectionChanging += OtherAddresses_CollectionChanging;
                    // create the event handler
                    var handler = BuildCollectionChangingHandler(instanceType, typeBuilder, baseType, fld, propertyChanging, setPropertyChanging);
                    code.Emit(OpCodes.Ldarg_0);
                    code.Emit(OpCodes.Ldfld, fld);
                    code.Emit(OpCodes.Ldarg_0);
                    code.Emit(OpCodes.Ldftn, handler);
                    code.Emit(OpCodes.Newobj, typeof(CollectionChangingHandler).GetConstructor(new Type[] { typeof(object), typeof(IntPtr) }));
                    code.Emit(OpCodes.Callvirt, typeof(INotifyCollectionChanging).GetEvent("CollectionChanging").AddMethod);
                    if (fld.FieldType.Implements<INotifyCollectionChanged>())
                    {
                        // _otherAddresses.CollectionChanging += OtherAddresses_CollectionChanging;
                        // create the event handler
                        var handler2 = BuildCollectionChangedHandler(instanceType, typeBuilder, baseType, fld, propertyChanging, setPropertyChanged);
                        code.Emit(OpCodes.Ldarg_0);
                        code.Emit(OpCodes.Ldfld, fld);
                        code.Emit(OpCodes.Ldarg_0);
                        code.Emit(OpCodes.Ldftn, handler2);
                        code.Emit(OpCodes.Newobj, typeof(NotifyCollectionChangedEventHandler).GetConstructor(new Type[] { typeof(object), typeof(IntPtr) }));
                        code.Emit(OpCodes.Callvirt, typeof(INotifyCollectionChanged).GetEvent("CollectionChanged").AddMethod);
                    }
                }
                else if (!fld.FieldType.IsArray)
                {
                    // _otherAddresses = new Flock<IAddress>();
                    code.Emit(OpCodes.Ldarg_0);
                    code.Emit(OpCodes.Newobj, fld.FieldType.GetConstructor(Type.EmptyTypes));
                    code.Emit(OpCodes.Stfld, fld);
                }
            }

            code.Emit(OpCodes.Ret);

            return ctor;
        }

        private static MethodInfo BuildSetPropertyChanging(Type instanceType, TypeBuilder typeBuilder)
        {
            /*
            private void SetPropertyChanging(IList items, PropertyChangingEventHandler handler, bool isAdd)
            {
                for (int i = 0; i < items.Count; i++)
                {
                    var item = items[i] as INotifyPropertyChanging;
                    if (item != null)
                    {
                        if (isAdd)
                        {
                            item.PropertyChanging += handler;
                        }
                        else
                        {
                            item.PropertyChanging -= handler;
                        }
                    }
                }
            }


            .method private hidebysig instance void  SetPropertyChanging(class [mscorlib]System.Collections.IList items,
                                                             class [System]System.ComponentModel.PropertyChangingEventHandler 'handler',
                                                             bool isAdd) cil managed
            {
              // Code size       81 (0x51)
              .maxstack  2
              .locals init ([0] int32 i,
                       [1] class [System]System.ComponentModel.INotifyPropertyChanging item,
                       [2] bool V_2,
                       [3] bool V_3,
                       [4] int32 V_4,
                       [5] bool V_5)
              IL_0000:  nop
              IL_0001:  ldc.i4.0
              IL_0002:  stloc.0
              IL_0003:  br.s       IL_0041
              IL_0005:  nop
              IL_0006:  ldarg.1
              IL_0007:  ldloc.0
              IL_0008:  callvirt   instance object [mscorlib]System.Collections.IList::get_Item(int32)
              IL_000d:  isinst     [System]System.ComponentModel.INotifyPropertyChanging
              IL_0012:  stloc.1
              IL_0013:  ldloc.1
              IL_0014:  ldnull
              IL_0015:  cgt.un
              IL_0017:  stloc.2
              IL_0018:  ldloc.2
              IL_0019:  brfalse.s  IL_0038
              IL_001b:  nop
              IL_001c:  ldarg.3
              IL_001d:  stloc.3
              IL_001e:  ldloc.3
              IL_001f:  brfalse.s  IL_002d
              IL_0021:  nop
              IL_0022:  ldloc.1
              IL_0023:  ldarg.2
              IL_0024:  callvirt   instance void [System]System.ComponentModel.INotifyPropertyChanging::add_PropertyChanging(class [System]System.ComponentModel.PropertyChangingEventHandler)
              IL_0029:  nop
              IL_002a:  nop
              IL_002b:  br.s       IL_0037
              IL_002d:  nop
              IL_002e:  ldloc.1
              IL_002f:  ldarg.2
              IL_0030:  callvirt   instance void [System]System.ComponentModel.INotifyPropertyChanging::remove_PropertyChanging(class [System]System.ComponentModel.PropertyChangingEventHandler)
              IL_0035:  nop
              IL_0036:  nop
              IL_0037:  nop
              IL_0038:  nop
              IL_0039:  ldloc.0
              IL_003a:  stloc.s    V_4
              IL_003c:  ldloc.s    V_4
              IL_003e:  ldc.i4.1
              IL_003f:  add
              IL_0040:  stloc.0
              IL_0041:  ldloc.0
              IL_0042:  ldarg.1
              IL_0043:  callvirt   instance int32 [mscorlib]System.Collections.ICollection::get_Count()
              IL_0048:  clt
              IL_004a:  stloc.s    V_5
              IL_004c:  ldloc.s    V_5
              IL_004e:  brtrue.s   IL_0005
              IL_0050:  ret
            } // end of method Patient::SetPropertyChanging
            */
            var method = typeBuilder.DefineMethod("SetPropertyChanging", 
                MethodAttributes.Private | MethodAttributes.HideBySig, 
                typeof(void), 
                new Type[] { typeof(IList), typeof(PropertyChangingEventHandler), typeof(bool) });
            var code = method.GetILGenerator();

            var i = code.DeclareLocal(typeof(int));
            var item = code.DeclareLocal(typeof(INotifyPropertyChanging));
            var v_2 = code.DeclareLocal(typeof(bool));
            var v_3 = code.DeclareLocal(typeof(bool));
            var v_4 = code.DeclareLocal(typeof(int));
            var v_5 = code.DeclareLocal(typeof(bool));

            code.Emit(OpCodes.Ldc_I4_0);
            code.Emit(OpCodes.Stloc, i);
            var loopTest = code.DefineLabel();
            code.Emit(OpCodes.Br_S, loopTest);
            var loopStart = code.DefineLabel();
            code.MarkLabel(loopStart);
            code.Emit(OpCodes.Ldarg_1); // the IList
            code.Emit(OpCodes.Ldloc_S, i);
            code.Emit(OpCodes.Callvirt, typeof(IList).GetMethod("get_Item", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic));
            code.Emit(OpCodes.Isinst, typeof(INotifyPropertyChanging));
            code.Emit(OpCodes.Stloc_S, item);
            code.Emit(OpCodes.Ldloc_S, item);
            code.Emit(OpCodes.Ldnull);
            code.Emit(OpCodes.Cgt_Un);
            code.Emit(OpCodes.Stloc_S, v_2);
            code.Emit(OpCodes.Ldloc_S, v_2);
            var endOfItem = code.DefineLabel();
            code.Emit(OpCodes.Brfalse_S, endOfItem);
            code.Emit(OpCodes.Ldarg_3);
            code.Emit(OpCodes.Stloc_S, v_3);
            code.Emit(OpCodes.Ldloc_S, v_3);
            var removeHandler = code.DefineLabel();
            code.Emit(OpCodes.Brfalse_S, removeHandler);
            code.Emit(OpCodes.Ldloc_S, item);
            code.Emit(OpCodes.Ldarg_2);
            code.Emit(OpCodes.Callvirt, typeof(INotifyPropertyChanging).GetEvent("PropertyChanging").AddMethod);
            code.Emit(OpCodes.Br_S, endOfItem);

            code.MarkLabel(removeHandler);
            code.Emit(OpCodes.Ldloc_S, item);
            code.Emit(OpCodes.Ldarg_2);
            code.Emit(OpCodes.Callvirt, typeof(INotifyPropertyChanging).GetEvent("PropertyChanging").RemoveMethod);

            code.MarkLabel(endOfItem);
            code.Emit(OpCodes.Ldloc_S, i);
            code.Emit(OpCodes.Stloc_S, v_4);
            code.Emit(OpCodes.Ldloc_S, v_4);
            code.Emit(OpCodes.Ldc_I4_1);
            code.Emit(OpCodes.Add);
            code.Emit(OpCodes.Stloc_S, i);

            code.MarkLabel(loopTest);
            code.Emit(OpCodes.Ldloc_S, i);
            code.Emit(OpCodes.Ldarg_1);
            code.Emit(OpCodes.Callvirt, typeof(ICollection).GetProperty("Count").GetMethod);
            code.Emit(OpCodes.Clt);
            code.Emit(OpCodes.Stloc_S, v_5);
            code.Emit(OpCodes.Ldloc_S, v_5);
            code.Emit(OpCodes.Brtrue_S, loopStart);

            code.Emit(OpCodes.Ret);

            return method;
        }

        private static MethodInfo BuildSetPropertyChanged(Type instanceType, TypeBuilder typeBuilder)
        {
            /*
            private void SetPropertyChanged(IList items, PropertyChangedEventHandler handler, bool isAdd)
            {
                for (int i = 0; i < items.Count; i++)
                {
                    var item = items[i] as INotifyPropertyChanged;
                    if (item != null)
                    {
                        if (isAdd)
                        {
                            item.PropertyChanged += handler;
                        }
                        else
                        {
                            item.PropertyChanged -= handler;
                        }
                    }
                }
            }


            .method private hidebysig instance void  SetPropertyChanging(class [mscorlib]System.Collections.IList items,
                                                             class [System]System.ComponentModel.PropertyChangingEventHandler 'handler',
                                                             bool isAdd) cil managed
            {
              // Code size       81 (0x51)
              .maxstack  2
              .locals init ([0] int32 i,
                       [1] class [System]System.ComponentModel.INotifyPropertyChanging item,
                       [2] bool V_2,
                       [3] bool V_3,
                       [4] int32 V_4,
                       [5] bool V_5)
              IL_0000:  nop
              IL_0001:  ldc.i4.0
              IL_0002:  stloc.0
              IL_0003:  br.s       IL_0041
              IL_0005:  nop
              IL_0006:  ldarg.1
              IL_0007:  ldloc.0
              IL_0008:  callvirt   instance object [mscorlib]System.Collections.IList::get_Item(int32)
              IL_000d:  isinst     [System]System.ComponentModel.INotifyPropertyChanging
              IL_0012:  stloc.1
              IL_0013:  ldloc.1
              IL_0014:  ldnull
              IL_0015:  cgt.un
              IL_0017:  stloc.2
              IL_0018:  ldloc.2
              IL_0019:  brfalse.s  IL_0038
              IL_001b:  nop
              IL_001c:  ldarg.3
              IL_001d:  stloc.3
              IL_001e:  ldloc.3
              IL_001f:  brfalse.s  IL_002d
              IL_0021:  nop
              IL_0022:  ldloc.1
              IL_0023:  ldarg.2
              IL_0024:  callvirt   instance void [System]System.ComponentModel.INotifyPropertyChanging::add_PropertyChanging(class [System]System.ComponentModel.PropertyChangingEventHandler)
              IL_0029:  nop
              IL_002a:  nop
              IL_002b:  br.s       IL_0037
              IL_002d:  nop
              IL_002e:  ldloc.1
              IL_002f:  ldarg.2
              IL_0030:  callvirt   instance void [System]System.ComponentModel.INotifyPropertyChanging::remove_PropertyChanging(class [System]System.ComponentModel.PropertyChangingEventHandler)
              IL_0035:  nop
              IL_0036:  nop
              IL_0037:  nop
              IL_0038:  nop
              IL_0039:  ldloc.0
              IL_003a:  stloc.s    V_4
              IL_003c:  ldloc.s    V_4
              IL_003e:  ldc.i4.1
              IL_003f:  add
              IL_0040:  stloc.0
              IL_0041:  ldloc.0
              IL_0042:  ldarg.1
              IL_0043:  callvirt   instance int32 [mscorlib]System.Collections.ICollection::get_Count()
              IL_0048:  clt
              IL_004a:  stloc.s    V_5
              IL_004c:  ldloc.s    V_5
              IL_004e:  brtrue.s   IL_0005
              IL_0050:  ret
            } // end of method Patient::SetPropertyChanging
            */
            var method = typeBuilder.DefineMethod("SetPropertyChanged",
                MethodAttributes.Private | MethodAttributes.HideBySig,
                typeof(void),
                new Type[] { typeof(IList), typeof(PropertyChangingEventHandler), typeof(bool) });
            var code = method.GetILGenerator();

            var i = code.DeclareLocal(typeof(int));
            var item = code.DeclareLocal(typeof(INotifyPropertyChanged));
            var v_2 = code.DeclareLocal(typeof(bool));
            var v_3 = code.DeclareLocal(typeof(bool));
            var v_4 = code.DeclareLocal(typeof(int));
            var v_5 = code.DeclareLocal(typeof(bool));

            code.Emit(OpCodes.Ldc_I4_0);
            code.Emit(OpCodes.Stloc, i);
            var loopTest = code.DefineLabel();
            code.Emit(OpCodes.Br_S, loopTest);
            var loopStart = code.DefineLabel();
            code.MarkLabel(loopStart);
            code.Emit(OpCodes.Ldarg_1); // the IList
            code.Emit(OpCodes.Ldloc_S, i);
            code.Emit(OpCodes.Callvirt, typeof(IList).GetMethod("get_Item", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic));
            code.Emit(OpCodes.Isinst, typeof(INotifyPropertyChanged));
            code.Emit(OpCodes.Stloc_S, item);
            code.Emit(OpCodes.Ldloc_S, item);
            code.Emit(OpCodes.Ldnull);
            code.Emit(OpCodes.Cgt_Un);
            code.Emit(OpCodes.Stloc_S, v_2);
            code.Emit(OpCodes.Ldloc_S, v_2);
            var endOfItem = code.DefineLabel();
            code.Emit(OpCodes.Brfalse_S, endOfItem);
            code.Emit(OpCodes.Ldarg_3);
            code.Emit(OpCodes.Stloc_S, v_3);
            code.Emit(OpCodes.Ldloc_S, v_3);
            var removeHandler = code.DefineLabel();
            code.Emit(OpCodes.Brfalse_S, removeHandler);
            code.Emit(OpCodes.Ldloc_S, item);
            code.Emit(OpCodes.Ldarg_2);
            code.Emit(OpCodes.Callvirt, typeof(INotifyPropertyChanged).GetEvent("PropertyChanged").AddMethod);
            code.Emit(OpCodes.Br_S, endOfItem);

            code.MarkLabel(removeHandler);
            code.Emit(OpCodes.Ldloc_S, item);
            code.Emit(OpCodes.Ldarg_2);
            code.Emit(OpCodes.Callvirt, typeof(INotifyPropertyChanged).GetEvent("PropertyChanged").RemoveMethod);

            code.MarkLabel(endOfItem);
            code.Emit(OpCodes.Ldloc_S, i);
            code.Emit(OpCodes.Stloc_S, v_4);
            code.Emit(OpCodes.Ldloc_S, v_4);
            code.Emit(OpCodes.Ldc_I4_1);
            code.Emit(OpCodes.Add);
            code.Emit(OpCodes.Stloc_S, i);

            code.MarkLabel(loopTest);
            code.Emit(OpCodes.Ldloc_S, i);
            code.Emit(OpCodes.Ldarg_1);
            code.Emit(OpCodes.Callvirt, typeof(ICollection).GetProperty("Count").GetMethod);
            code.Emit(OpCodes.Clt);
            code.Emit(OpCodes.Stloc_S, v_5);
            code.Emit(OpCodes.Ldloc_S, v_5);
            code.Emit(OpCodes.Brtrue_S, loopStart);

            code.Emit(OpCodes.Ret);

            return method;
        }

        private static MethodInfo BuildCollectionChangedHandler(Type interfaceType, TypeBuilder typeBuilder, Type baseType, FieldInfo fld, FieldInfo propertyChanged, MethodInfo setPropertyChanged)
        {
            /* 
            private void OtherAddresses_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
            {
                switch(e.Action)
                {
                    case System.Collections.Specialized.NotifyCollectionChangedAction.Add:
                        {
                            SetPropertyChanged(e.NewItems, OtherAddresses_PropertyChanging, true);
                            break;
                        }
                    case System.Collections.Specialized.NotifyCollectionChangedAction.Remove:
                        {
                            SetPropertyChanged(e.OldItems, OtherAddresses_PropertyChanging, false);
                            break;
                        }
                    case System.Collections.Specialized.NotifyCollectionChangedAction.Replace:
                        {
                            SetPropertyChanged(e.OldItems, OtherAddresses_PropertyChanging, false);
                            SetPropertyChanged(e.NewItems, OtherAddresses_PropertyChanging, true);
                            break;
                        }
                    case System.Collections.Specialized.NotifyCollectionChangedAction.Reset:
                        {
                            SetPropertyChanged(_otherAddresses.ToList(), OtherAddresses_PropertyChanging, false);
                            break;
                        }
                }
                OnPropertyChanged(string.Format("{0}.{1}({2},{3},{4},{5})",
                    "OtherAddresses",
                    e.Action,
                    e.NewStartingIndex,
                    e.NewItems?.Count ?? 0,
                    e.OldStartingIndex,
                    e.OldItems?.Count ?? 0
                    ));
                OnPropertyChanged("OtherAddresses");
            }


            .method private hidebysig instance void  OtherAddresses_CollectionChanged(object sender, class [Common]Common.Collections.CollectionChangingEventArgs e) cil managed
            {
              // Code size       199 (0xc7)
              .maxstack  4
              .locals init ([0] valuetype [System]System.Collections.Specialized.NotifyCollectionChangedAction V_0)
              IL_0000:  nop
              IL_0001:  ldarg.2
              IL_0002:  callvirt   instance valuetype [System]System.Collections.Specialized.NotifyCollectionChangedAction [Common]Common.Collections.CollectionChangingEventArgs::get_Action()
              IL_0007:  stloc.0
              IL_0008:  ldloc.0
              IL_0009:  switch     ( 
                                    IL_0027,
                                    IL_0044,
                                    IL_0061,
                                    IL_00ba,
                                    IL_0098)
              IL_0022:  br         IL_00ba
              IL_0027:  nop
              IL_0028:  ldarg.0
              IL_0029:  ldarg.2
              IL_002a:  callvirt   instance class [mscorlib]System.Collections.IList [Common]Common.Collections.CollectionChangingEventArgs::get_NewItems()
              IL_002f:  ldarg.0
              IL_0030:  ldftn      instance void Data.Core.Tests.Patient::OtherAddresses_PropertyChanging(object,
                                                                                                              class [System]System.ComponentModel.PropertyChangingEventArgs)
              IL_0036:  newobj     instance void [System]System.ComponentModel.PropertyChangingEventHandler::.ctor(object,
                                                                                                                   native int)
              IL_003b:  ldc.i4.1
              IL_003c:  call       instance void Data.Core.Tests.Patient::SetPropertyChanging(class [mscorlib]System.Collections.IList,
                                                                                                  class [System]System.ComponentModel.PropertyChangingEventHandler,
                                                                                                  bool)
              IL_0041:  nop
              IL_0042:  br.s       IL_00ba
              IL_0044:  nop
              IL_0045:  ldarg.0
              IL_0046:  ldarg.2
              IL_0047:  callvirt   instance class [mscorlib]System.Collections.IList [Common]Common.Collections.CollectionChangingEventArgs::get_OldItems()
              IL_004c:  ldarg.0
              IL_004d:  ldftn      instance void Data.Core.Tests.Patient::OtherAddresses_PropertyChanging(object,
                                                                                                              class [System]System.ComponentModel.PropertyChangingEventArgs)
              IL_0053:  newobj     instance void [System]System.ComponentModel.PropertyChangingEventHandler::.ctor(object,
                                                                                                                   native int)
              IL_0058:  ldc.i4.0
              IL_0059:  call       instance void Data.Core.Tests.Patient::SetPropertyChanging(class [mscorlib]System.Collections.IList,
                                                                                                  class [System]System.ComponentModel.PropertyChangingEventHandler,
                                                                                                  bool)
              IL_005e:  nop
              IL_005f:  br.s       IL_00ba
              IL_0061:  nop
              IL_0062:  ldarg.0
              IL_0063:  ldarg.2
              IL_0064:  callvirt   instance class [mscorlib]System.Collections.IList [Common]Common.Collections.CollectionChangingEventArgs::get_OldItems()
              IL_0069:  ldarg.0
              IL_006a:  ldftn      instance void Data.Core.Tests.Patient::OtherAddresses_PropertyChanging(object,
                                                                                                              class [System]System.ComponentModel.PropertyChangingEventArgs)
              IL_0070:  newobj     instance void [System]System.ComponentModel.PropertyChangingEventHandler::.ctor(object,
                                                                                                                   native int)
              IL_0075:  ldc.i4.0
              IL_0076:  call       instance void Data.Core.Tests.Patient::SetPropertyChanging(class [mscorlib]System.Collections.IList,
                                                                                                  class [System]System.ComponentModel.PropertyChangingEventHandler,
                                                                                                  bool)
              IL_007b:  nop
              IL_007c:  ldarg.0
              IL_007d:  ldarg.2
              IL_007e:  callvirt   instance class [mscorlib]System.Collections.IList [Common]Common.Collections.CollectionChangingEventArgs::get_NewItems()
              IL_0083:  ldarg.0
              IL_0084:  ldftn      instance void Data.Core.Tests.Patient::OtherAddresses_PropertyChanging(object,
                                                                                                              class [System]System.ComponentModel.PropertyChangingEventArgs)
              IL_008a:  newobj     instance void [System]System.ComponentModel.PropertyChangingEventHandler::.ctor(object,
                                                                                                                   native int)
              IL_008f:  ldc.i4.1
              IL_0090:  call       instance void Data.Core.Tests.Patient::SetPropertyChanging(class [mscorlib]System.Collections.IList,
                                                                                                  class [System]System.ComponentModel.PropertyChangingEventHandler,
                                                                                                  bool)
              IL_0095:  nop
              IL_0096:  br.s       IL_00ba
              IL_0098:  nop
              IL_0099:  ldarg.0
              IL_009a:  ldarg.0
              IL_009b:  ldfld      class [Common]Common.Collections.Flock`1<class Data.Core.Tests.IAddress> Data.Core.Tests.Patient::_otherAddresses
              IL_00a0:  call       class [mscorlib]System.Collections.Generic.List`1<!!0> [System.Core]System.Linq.Enumerable::ToList<class Data.Core.Tests.IAddress>(class [mscorlib]System.Collections.Generic.IEnumerable`1<!!0>)
              IL_00a5:  ldarg.0
              IL_00a6:  ldftn      instance void Data.Core.Tests.Patient::OtherAddresses_PropertyChanging(object,
                                                                                                              class [System]System.ComponentModel.PropertyChangingEventArgs)
              IL_00ac:  newobj     instance void [System]System.ComponentModel.PropertyChangingEventHandler::.ctor(object,
                                                                                                                   native int)
              IL_00b1:  ldc.i4.0
              IL_00b2:  call       instance void Data.Core.Tests.Patient::SetPropertyChanging(class [mscorlib]System.Collections.IList,
                                                                                                  class [System]System.ComponentModel.PropertyChangingEventHandler,
                                                                                                  bool)
                                                                                                  IL_0000:  nop
              

              IL_00b7:  nop
              IL_00b8:  br.s       IL_00ba
              L_00ba:  ldarg.0
              // build formatted string IL
              IL_0079:  callvirt   instance void class [Data.Core]Data.Core.Dynamic.Extendable`1<class [UX.Core]UX.Core.ViewModelBase>::OnPropertyChanging(string)
              IL_00ba:  ldarg.0
              IL_00bb:  ldstr      "OtherAddresses"
              IL_00c0:  callvirt   instance void class [Data.Core]Data.Core.Dynamic.Extendable`1<class [UX.Core]UX.Core.ViewModelBase>::OnPropertyChanging(string)
              IL_00c5:  nop
              IL_00c6:  ret
            } // end of method Patient::OtherAddresses_CollectionChanging
            */
            var itemPropertyChanged = BuildCollectionItemPropertyChangedHandler(interfaceType, typeBuilder, baseType, fld, propertyChanged);
            var method = typeBuilder.DefineMethod(fld.Name + "_CollectionChanged", MethodAttributes.Private | MethodAttributes.HideBySig,
                typeof(void),
                new Type[] { typeof(object), typeof(NotifyCollectionChangedEventArgs) });

            var code = method.GetILGenerator();
            var v_0 = code.DeclareLocal(typeof(NotifyCollectionChangedAction));
            var endOfSwitch = code.DefineLabel();
            code.Emit(OpCodes.Ldarg_2);
            code.Emit(OpCodes.Callvirt, typeof(NotifyCollectionChangedEventArgs).GetProperty("Action").GetMethod);
            code.Emit(OpCodes.Stloc, v_0);
            code.Emit(OpCodes.Ldloc, v_0);
            var jumpTable = new Label[]
            {
                code.DefineLabel(), // add
                code.DefineLabel(), // remove
                code.DefineLabel(), // replace
                endOfSwitch, // move
                code.DefineLabel()  // reset
            };
            code.Emit(OpCodes.Switch, jumpTable);

            code.Emit(OpCodes.Br, endOfSwitch);
            // case System.Collections.Specialized.NotifyCollectionChangedAction.Add:
            // {
            //     SetPropertyChanging(e.NewItems, OtherAddresses_PropertyChanging, true);
            //     break;
            // }
            code.MarkLabel(jumpTable[0]);
            code.Emit(OpCodes.Ldarg_0);
            code.Emit(OpCodes.Ldarg_2);
            code.Emit(OpCodes.Callvirt, typeof(NotifyCollectionChangedEventArgs).GetProperty("NewItems").GetMethod);
            code.Emit(OpCodes.Ldarg_0);
            code.Emit(OpCodes.Ldftn, itemPropertyChanged);
            code.Emit(OpCodes.Newobj, typeof(PropertyChangedEventHandler).GetConstructor(new Type[] { typeof(object), typeof(IntPtr) }));
            code.Emit(OpCodes.Ldc_I4_1);
            code.Emit(OpCodes.Call, setPropertyChanged);
            code.Emit(OpCodes.Br_S, endOfSwitch);

            // case System.Collections.Specialized.NotifyCollectionChangedAction.Remove:
            // {
            //     SetPropertyChanging(e.OldItems, OtherAddresses_PropertyChanging, false);
            //     break;
            // }
            code.MarkLabel(jumpTable[1]);
            code.Emit(OpCodes.Ldarg_0);
            code.Emit(OpCodes.Ldarg_2);
            code.Emit(OpCodes.Callvirt, typeof(NotifyCollectionChangedEventArgs).GetProperty("OldItems").GetMethod);
            code.Emit(OpCodes.Ldarg_0);
            code.Emit(OpCodes.Ldftn, itemPropertyChanged);
            code.Emit(OpCodes.Newobj, typeof(PropertyChangedEventHandler).GetConstructor(new Type[] { typeof(object), typeof(IntPtr) }));
            code.Emit(OpCodes.Ldc_I4_0);
            code.Emit(OpCodes.Call, setPropertyChanged);
            code.Emit(OpCodes.Br_S, endOfSwitch);

            // case System.Collections.Specialized.NotifyCollectionChangedAction.Replace:
            //             {
            //     SetPropertyChanging(e.OldItems, OtherAddresses_PropertyChanging, false);
            //     SetPropertyChanging(e.NewItems, OtherAddresses_PropertyChanging, true);
            //     break;
            // }
            code.MarkLabel(jumpTable[2]);
            code.Emit(OpCodes.Ldarg_0);
            code.Emit(OpCodes.Ldarg_2);
            code.Emit(OpCodes.Callvirt, typeof(NotifyCollectionChangedEventArgs).GetProperty("OldItems").GetMethod);
            code.Emit(OpCodes.Ldarg_0);
            code.Emit(OpCodes.Ldftn, itemPropertyChanged);
            code.Emit(OpCodes.Newobj, typeof(PropertyChangedEventHandler).GetConstructor(new Type[] { typeof(object), typeof(IntPtr) }));
            code.Emit(OpCodes.Ldc_I4_0);
            code.Emit(OpCodes.Call, setPropertyChanged);
            code.Emit(OpCodes.Ldarg_0);
            code.Emit(OpCodes.Ldarg_2);
            code.Emit(OpCodes.Callvirt, typeof(NotifyCollectionChangedEventArgs).GetProperty("NewItems").GetMethod);
            code.Emit(OpCodes.Ldarg_0);
            code.Emit(OpCodes.Ldftn, itemPropertyChanged);
            code.Emit(OpCodes.Newobj, typeof(PropertyChangedEventHandler).GetConstructor(new Type[] { typeof(object), typeof(IntPtr) }));
            code.Emit(OpCodes.Ldc_I4_1);
            code.Emit(OpCodes.Call, setPropertyChanged);
            code.Emit(OpCodes.Br_S, endOfSwitch);

            // case System.Collections.Specialized.NotifyCollectionChangedAction.Reset:
            //             {
            //     SetPropertyChanging(_otherAddresses.ToList(), OtherAddresses_PropertyChanging, false);
            //     break;
            // }
            code.MarkLabel(jumpTable[4]);
            code.Emit(OpCodes.Ldarg_0);
            code.Emit(OpCodes.Ldarg_0);
            code.Emit(OpCodes.Ldfld, fld);
            code.Emit(OpCodes.Call, typeof(Enumerable).GetMethod("ToList", BindingFlags.Public | BindingFlags.Static).MakeGenericMethod(fld.FieldType.GetGenericArguments()[0]));
            code.Emit(OpCodes.Ldarg_0);
            code.Emit(OpCodes.Ldftn, itemPropertyChanged);
            code.Emit(OpCodes.Newobj, typeof(PropertyChangedEventHandler).GetConstructor(new Type[] { typeof(object), typeof(IntPtr) }));
            code.Emit(OpCodes.Ldc_I4_0);
            code.Emit(OpCodes.Call, setPropertyChanged);
            code.Emit(OpCodes.Br_S, endOfSwitch);


            code.MarkLabel(endOfSwitch);

            MethodInfo onPropertyChangedMI = null;
            if (baseType != null && baseType.FindInterfaces(
                (type, filter) => type.FullName.Contains(filter.ToString()),
                typeof(INotifyPropertyChanged).FullName).Any())
            {
                onPropertyChangedMI = baseType.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                    .SingleOrDefault(mi =>
                           mi.Name.Equals("OnPropertyChanged")
                        && mi.GetParameters().Length == 1
                        && mi.GetParameters()[0].ParameterType.Equals(typeof(string)));
            }

            if (onPropertyChangedMI == null)
            {
                // implement the ProperyChanged event raise
                var noSubs = code.DefineLabel();
                code.Emit(OpCodes.Ldarg_0);
                code.Emit(OpCodes.Ldfld, propertyChanged);
                code.Emit(OpCodes.Ldnull);
                code.Emit(OpCodes.Cgt_Un);
                code.Emit(OpCodes.Brfalse_S, noSubs);
                code.Emit(OpCodes.Ldarg_0);
                BuildFormattedCollectionChangedPropertyString(code, fld.Name.Substring(1));
                code.Emit(OpCodes.Newobj, typeof(PropertyChangedEventArgs).GetConstructor(new[] { typeof(string) }));
                code.Emit(OpCodes.Callvirt, typeof(PropertyChangedEventHandler).GetMethod("Invoke", BindingFlags.Public | BindingFlags.Instance));
                code.Emit(OpCodes.Ldarg_0);
                code.Emit(OpCodes.Ldstr, fld.Name.Substring(1));
                code.Emit(OpCodes.Newobj, typeof(PropertyChangedEventArgs).GetConstructor(new[] { typeof(string) }));
                code.Emit(OpCodes.Callvirt, typeof(PropertyChangedEventHandler).GetMethod("Invoke", BindingFlags.Public | BindingFlags.Instance));
                code.MarkLabel(noSubs);
            }
            else
            {
                code.Emit(OpCodes.Ldarg_0);
                BuildFormattedCollectionChangedPropertyString(code, fld.Name.Substring(1));
                code.Emit(OpCodes.Callvirt, onPropertyChangedMI);
                code.Emit(OpCodes.Ldarg_0);
                code.Emit(OpCodes.Ldstr, fld.Name.Substring(1));
                code.Emit(OpCodes.Callvirt, onPropertyChangedMI);
            }

            code.Emit(OpCodes.Ret);

            return method;
        }

        private static void BuildFormattedCollectionChangedPropertyString(ILGenerator code, string propertyName)
        {
            /*
             * 
            string.Format("{0}.{1}({2},{3},{4},{5})",
                this.Name,
                e.Action,
                e.NewStartingIndex,
                e.NewItems?.Count ?? 0,
                e.OldStartingIndex,
                e.OldItems?.Count ?? 0
                )
             
            IL_0000:  nop
              IL_0001:  ldstr      "{0}.{1}({2},{3},{4},{5})"
              IL_0006:  ldc.i4.6
              IL_0007:  newarr     [mscorlib]System.Object
              IL_000c:  dup
              IL_000d:  ldc.i4.0
              IL_000e:  ldarg.0
              IL_000f:  call       instance string class Data.Core.Dynamic.DynamicProperty`1<!T>::get_Name()
              IL_0014:  stelem.ref
              IL_0015:  dup
              IL_0016:  ldc.i4.1
              IL_0017:  ldarg.2
              IL_0018:  callvirt   instance valuetype [System]System.Collections.Specialized.NotifyCollectionChangedAction [System]System.Collections.Specialized.NotifyCollectionChangedEventArgs::get_Action()
              IL_001d:  box        [System]System.Collections.Specialized.NotifyCollectionChangedAction
              IL_0022:  stelem.ref
              IL_0023:  dup
              IL_0024:  ldc.i4.2
              IL_0025:  ldarg.2
              IL_0026:  callvirt   instance int32 [System]System.Collections.Specialized.NotifyCollectionChangedEventArgs::get_NewStartingIndex()
              IL_002b:  box        [mscorlib]System.Int32
              IL_0030:  stelem.ref
              IL_0031:  dup
              IL_0032:  ldc.i4.3
              IL_0033:  ldarg.2
              IL_0034:  callvirt   instance class [mscorlib]System.Collections.IList [System]System.Collections.Specialized.NotifyCollectionChangedEventArgs::get_NewItems()
              IL_0039:  dup
              IL_003a:  brtrue.s   IL_0040
              IL_003c:  pop
              IL_003d:  ldc.i4.0
              IL_003e:  br.s       IL_0045
              IL_0040:  callvirt   instance int32 [mscorlib]System.Collections.ICollection::get_Count()
              IL_0045:  box        [mscorlib]System.Int32
              IL_004a:  stelem.ref
              IL_004b:  dup
              IL_004c:  ldc.i4.4
              IL_004d:  ldarg.2
              IL_004e:  callvirt   instance int32 [System]System.Collections.Specialized.NotifyCollectionChangedEventArgs::get_OldStartingIndex()
              IL_0053:  box        [mscorlib]System.Int32
              IL_0058:  stelem.ref
              IL_0059:  dup
              IL_005a:  ldc.i4.5
              IL_005b:  ldarg.2
              IL_005c:  callvirt   instance class [mscorlib]System.Collections.IList [System]System.Collections.Specialized.NotifyCollectionChangedEventArgs::get_OldItems()
              IL_0061:  dup
              IL_0062:  brtrue.s   IL_0068
              IL_0064:  pop
              IL_0065:  ldc.i4.0
              IL_0066:  br.s       IL_006d
              IL_0068:  callvirt   instance int32 [mscorlib]System.Collections.ICollection::get_Count()
              IL_006d:  box        [mscorlib]System.Int32
              IL_0072:  stelem.ref
              IL_0073:  call       string [mscorlib]System.String::Format(string,
                                                                          object[])
              IL_0078:  stloc.0
              IL_0079:  br.s       IL_007b
              IL_007b:  ldloc.0


            */

            code.Emit(OpCodes.Ldstr, "{0}.{1}({2},{3},{4},{5})");
            code.Emit(OpCodes.Ldc_I4_6);
            code.Emit(OpCodes.Newarr, typeof(object));
            code.Emit(OpCodes.Dup);

            code.Emit(OpCodes.Ldc_I4_0);
            code.Emit(OpCodes.Ldstr, propertyName);
            code.Emit(OpCodes.Stelem_Ref);
            code.Emit(OpCodes.Dup);

            code.Emit(OpCodes.Ldc_I4_1);
            code.Emit(OpCodes.Ldarg_2);
            code.Emit(OpCodes.Callvirt, typeof(NotifyCollectionChangedEventArgs).GetProperty("Action").GetMethod);
            code.Emit(OpCodes.Box, typeof(NotifyCollectionChangedAction));
            code.Emit(OpCodes.Stelem_Ref);
            code.Emit(OpCodes.Dup);

            code.Emit(OpCodes.Ldc_I4_2);
            code.Emit(OpCodes.Ldarg_2);
            code.Emit(OpCodes.Callvirt, typeof(NotifyCollectionChangedEventArgs).GetProperty("NewStartingIndex").GetMethod);
            code.Emit(OpCodes.Box, typeof(int));
            code.Emit(OpCodes.Stelem_Ref);
            code.Emit(OpCodes.Dup);

            code.Emit(OpCodes.Ldc_I4_3);
            code.Emit(OpCodes.Ldarg_2);
            code.Emit(OpCodes.Callvirt, typeof(NotifyCollectionChangedEventArgs).GetProperty("NewItems").GetMethod);
            code.Emit(OpCodes.Dup);
            var newItemsNotNull = code.DefineLabel();
            code.Emit(OpCodes.Brtrue_S, newItemsNotNull);
            code.Emit(OpCodes.Pop);
            code.Emit(OpCodes.Ldc_I4_0);
            var newItemsNull = code.DefineLabel();
            code.Emit(OpCodes.Br_S, newItemsNull);
            code.MarkLabel(newItemsNotNull);
            code.Emit(OpCodes.Callvirt, typeof(ICollection).GetProperty("Count").GetMethod);
            code.MarkLabel(newItemsNull);
            code.Emit(OpCodes.Box, typeof(int));
            code.Emit(OpCodes.Stelem_Ref);
            code.Emit(OpCodes.Dup);

            code.Emit(OpCodes.Ldc_I4_4);
            code.Emit(OpCodes.Ldarg_2);
            code.Emit(OpCodes.Callvirt, typeof(NotifyCollectionChangedEventArgs).GetProperty("OldStartingIndex").GetMethod);
            code.Emit(OpCodes.Box, typeof(int));
            code.Emit(OpCodes.Stelem_Ref);
            code.Emit(OpCodes.Dup);

            code.Emit(OpCodes.Ldc_I4_5);
            code.Emit(OpCodes.Ldarg_2);
            code.Emit(OpCodes.Callvirt, typeof(NotifyCollectionChangedEventArgs).GetProperty("OldItems").GetMethod);
            code.Emit(OpCodes.Dup);
            var oldItemsNotNull = code.DefineLabel();
            code.Emit(OpCodes.Brtrue_S, oldItemsNotNull);
            code.Emit(OpCodes.Pop);
            code.Emit(OpCodes.Ldc_I4_0);
            var oldItemsNull = code.DefineLabel();
            code.Emit(OpCodes.Br_S, oldItemsNull);
            code.MarkLabel(oldItemsNotNull);
            code.Emit(OpCodes.Callvirt, typeof(ICollection).GetProperty("Count").GetMethod);
            code.MarkLabel(oldItemsNull);
            code.Emit(OpCodes.Box, typeof(int));
            code.Emit(OpCodes.Stelem_Ref);

            code.Emit(OpCodes.Call, typeof(String).GetMethods(BindingFlags.Static | BindingFlags.Public)
                .Single(m => m.Name.Equals("Format") && m.GetParameters().Length == 2 && m.GetParameters()[1].ParameterType.Equals(typeof(object[]))));
        }

        private static void BuildFormattedCollectionChangingPropertyString(ILGenerator code, string propertyName)
        {
            /*
             * 
            string.Format("{0}.{1}({2},{3},{4},{5})",
                this.Name,
                e.Action,
                e.NewStartingIndex,
                e.NewItems?.Count ?? 0,
                e.OldStartingIndex,
                e.OldItems?.Count ?? 0
                )
             
            IL_0001:  ldarg.0
              IL_0002:  ldstr      "{0}.{1}({2},{3},{4},{5})"
              IL_0007:  ldc.i4.6
              IL_0008:  newarr     [mscorlib]System.Object
              IL_000d:  dup
              IL_000e:  ldc.i4.0
              IL_000f:  ldarg.0
              IL_0010:  call       instance string class Data.Core.Dynamic.DynamicProperty`1<!T>::get_Name()
              IL_0015:  stelem.ref
              IL_0016:  dup
              IL_0017:  ldc.i4.1
              IL_0018:  ldarg.2
              IL_0019:  callvirt   instance valuetype [System]System.Collections.Specialized.NotifyCollectionChangedAction [System]System.Collections.Specialized.NotifyCollectionChangedEventArgs::get_Action()
              IL_001e:  box        [System]System.Collections.Specialized.NotifyCollectionChangedAction
              IL_0023:  stelem.ref
              IL_0024:  dup
              IL_0025:  ldc.i4.2
              IL_0026:  ldarg.2
              IL_0027:  callvirt   instance int32 [System]System.Collections.Specialized.NotifyCollectionChangedEventArgs::get_NewStartingIndex()
              IL_002c:  box        [mscorlib]System.Int32
              IL_0031:  stelem.ref
              IL_0032:  dup
              IL_0033:  ldc.i4.3
              IL_0034:  ldarg.2
              IL_0035:  callvirt   instance class [mscorlib]System.Collections.IList [System]System.Collections.Specialized.NotifyCollectionChangedEventArgs::get_NewItems()
              IL_003a:  dup
              IL_003b:  brtrue.s   IL_0041
              IL_003d:  pop
              IL_003e:  ldc.i4.0
              IL_003f:  br.s       IL_0046
              IL_0041:  callvirt   instance int32 [mscorlib]System.Collections.ICollection::get_Count()
              IL_0046:  box        [mscorlib]System.Int32
              IL_004b:  stelem.ref
              IL_004c:  dup
              IL_004d:  ldc.i4.4
              IL_004e:  ldarg.2
              IL_004f:  callvirt   instance int32 [System]System.Collections.Specialized.NotifyCollectionChangedEventArgs::get_OldStartingIndex()
              IL_0054:  box        [mscorlib]System.Int32
              IL_0059:  stelem.ref
              IL_005a:  dup
              IL_005b:  ldc.i4.5
              IL_005c:  ldarg.2
              IL_005d:  callvirt   instance class [mscorlib]System.Collections.IList [System]System.Collections.Specialized.NotifyCollectionChangedEventArgs::get_OldItems()
              IL_0062:  dup
              IL_0063:  brtrue.s   IL_0069
              IL_0065:  pop
              IL_0066:  ldc.i4.0
              IL_0067:  br.s       IL_006e
              IL_0069:  callvirt   instance int32 [mscorlib]System.Collections.ICollection::get_Count()
              IL_006e:  box        [mscorlib]System.Int32
              IL_0073:  stelem.ref
              IL_0074:  call       string [mscorlib]System.String::Format(string,
                                                                          object[])

            */

            code.Emit(OpCodes.Ldstr, "{0}.{1}({2},{3},{4},{5})");
            code.Emit(OpCodes.Ldc_I4_6);
            code.Emit(OpCodes.Newarr, typeof(object));
            code.Emit(OpCodes.Dup);

            code.Emit(OpCodes.Ldc_I4_0);
            code.Emit(OpCodes.Ldstr, propertyName);
            code.Emit(OpCodes.Stelem_Ref);
            code.Emit(OpCodes.Dup);

            code.Emit(OpCodes.Ldc_I4_1);
            code.Emit(OpCodes.Ldarg_2);
            code.Emit(OpCodes.Callvirt, typeof(CollectionChangingEventArgs).GetProperty("Action").GetMethod);
            code.Emit(OpCodes.Box, typeof(NotifyCollectionChangedAction));
            code.Emit(OpCodes.Stelem_Ref);
            code.Emit(OpCodes.Dup);

            code.Emit(OpCodes.Ldc_I4_2);
            code.Emit(OpCodes.Ldarg_2);
            code.Emit(OpCodes.Callvirt, typeof(CollectionChangingEventArgs).GetProperty("NewStartingIndex").GetMethod);
            code.Emit(OpCodes.Box, typeof(int));
            code.Emit(OpCodes.Stelem_Ref);
            code.Emit(OpCodes.Dup);

            code.Emit(OpCodes.Ldc_I4_3);
            code.Emit(OpCodes.Ldarg_2);
            code.Emit(OpCodes.Callvirt, typeof(CollectionChangingEventArgs).GetProperty("NewItems").GetMethod);
            code.Emit(OpCodes.Dup);
            var newItemsNotNull = code.DefineLabel();
            code.Emit(OpCodes.Brtrue_S, newItemsNotNull);
            code.Emit(OpCodes.Pop);
            code.Emit(OpCodes.Ldc_I4_0);
            var newItemsNull = code.DefineLabel();
            code.Emit(OpCodes.Br_S, newItemsNull);
            code.MarkLabel(newItemsNotNull);
            code.Emit(OpCodes.Callvirt, typeof(ICollection).GetProperty("Count").GetMethod);
            code.MarkLabel(newItemsNull);
            code.Emit(OpCodes.Box, typeof(int));
            code.Emit(OpCodes.Stelem_Ref);
            code.Emit(OpCodes.Dup);

            code.Emit(OpCodes.Ldc_I4_4);
            code.Emit(OpCodes.Ldarg_2);
            code.Emit(OpCodes.Callvirt, typeof(CollectionChangingEventArgs).GetProperty("OldStartingIndex").GetMethod);
            code.Emit(OpCodes.Box, typeof(int));
            code.Emit(OpCodes.Stelem_Ref);
            code.Emit(OpCodes.Dup);

            code.Emit(OpCodes.Ldc_I4_5);
            code.Emit(OpCodes.Ldarg_2);
            code.Emit(OpCodes.Callvirt, typeof(CollectionChangingEventArgs).GetProperty("OldItems").GetMethod);
            code.Emit(OpCodes.Dup);
            var oldItemsNotNull = code.DefineLabel();
            code.Emit(OpCodes.Brtrue_S, oldItemsNotNull);
            code.Emit(OpCodes.Pop);
            code.Emit(OpCodes.Ldc_I4_0);
            var oldItemsNull = code.DefineLabel();
            code.Emit(OpCodes.Br_S, oldItemsNull);
            code.MarkLabel(oldItemsNotNull);
            code.Emit(OpCodes.Callvirt, typeof(ICollection).GetProperty("Count").GetMethod);
            code.MarkLabel(oldItemsNull);
            code.Emit(OpCodes.Box, typeof(int));
            code.Emit(OpCodes.Stelem_Ref);

            code.Emit(OpCodes.Call, typeof(String).GetMethods(BindingFlags.Static | BindingFlags.Public)
                .Single(m => m.Name.Equals("Format") && m.GetParameters().Length == 2 && m.GetParameters()[1].ParameterType.Equals(typeof(object[]))));
        }

        private static MethodInfo BuildCollectionChangingHandler(Type interfaceType, TypeBuilder typeBuilder, Type baseType, FieldInfo fld, FieldInfo propertyChanging, MethodInfo setPropertyChanging)
        {
            /* 
            private void OtherAddresses_CollectionChanging(object sender, CollectionChangingEventArgs e)
            {
                switch(e.Action)
                {
                    case System.Collections.Specialized.NotifyCollectionChangedAction.Add:
                        {
                            SetPropertyChanging(e.NewItems, OtherAddresses_PropertyChanging, true);
                            break;
                        }
                    case System.Collections.Specialized.NotifyCollectionChangedAction.Remove:
                        {
                            SetPropertyChanging(e.OldItems, OtherAddresses_PropertyChanging, false);
                            break;
                        }
                    case System.Collections.Specialized.NotifyCollectionChangedAction.Replace:
                        {
                            SetPropertyChanging(e.OldItems, OtherAddresses_PropertyChanging, false);
                            SetPropertyChanging(e.NewItems, OtherAddresses_PropertyChanging, true);
                            break;
                        }
                    case System.Collections.Specialized.NotifyCollectionChangedAction.Reset:
                        {
                            SetPropertyChanging(_otherAddresses.ToList(), OtherAddresses_PropertyChanging, false);
                            break;
                        }
                }
                 OnPropertyChanging(string.Format("{0}.{1}({2},{3},{4},{5})",
                    "OtherAddresses",
                    e.Action,
                    e.NewStartingIndex,
                    e.NewItems?.Count ?? 0,
                    e.OldStartingIndex,
                    e.OldItems?.Count ?? 0
                    ));
                OnPropertyChanging("OtherAddresses");
            }


            .method private hidebysig instance void  OtherAddresses_CollectionChanging(object sender, class [Common]Common.Collections.CollectionChangingEventArgs e) cil managed
            {
              // Code size       199 (0xc7)
              .maxstack  4
              .locals init ([0] valuetype [System]System.Collections.Specialized.NotifyCollectionChangedAction V_0)
              IL_0000:  nop
              IL_0001:  ldarg.2
              IL_0002:  callvirt   instance valuetype [System]System.Collections.Specialized.NotifyCollectionChangedAction [Common]Common.Collections.CollectionChangingEventArgs::get_Action()
              IL_0007:  stloc.0
              IL_0008:  ldloc.0
              IL_0009:  switch     ( 
                                    IL_0027,
                                    IL_0044,
                                    IL_0061,
                                    IL_00ba,
                                    IL_0098)
              IL_0022:  br         IL_00ba
              IL_0027:  nop
              IL_0028:  ldarg.0
              IL_0029:  ldarg.2
              IL_002a:  callvirt   instance class [mscorlib]System.Collections.IList [Common]Common.Collections.CollectionChangingEventArgs::get_NewItems()
              IL_002f:  ldarg.0
              IL_0030:  ldftn      instance void Data.Core.Tests.Patient::OtherAddresses_PropertyChanging(object,
                                                                                                              class [System]System.ComponentModel.PropertyChangingEventArgs)
              IL_0036:  newobj     instance void [System]System.ComponentModel.PropertyChangingEventHandler::.ctor(object,
                                                                                                                   native int)
              IL_003b:  ldc.i4.1
              IL_003c:  call       instance void Data.Core.Tests.Patient::SetPropertyChanging(class [mscorlib]System.Collections.IList,
                                                                                                  class [System]System.ComponentModel.PropertyChangingEventHandler,
                                                                                                  bool)
              IL_0041:  nop
              IL_0042:  br.s       IL_00ba
              IL_0044:  nop
              IL_0045:  ldarg.0
              IL_0046:  ldarg.2
              IL_0047:  callvirt   instance class [mscorlib]System.Collections.IList [Common]Common.Collections.CollectionChangingEventArgs::get_OldItems()
              IL_004c:  ldarg.0
              IL_004d:  ldftn      instance void Data.Core.Tests.Patient::OtherAddresses_PropertyChanging(object,
                                                                                                              class [System]System.ComponentModel.PropertyChangingEventArgs)
              IL_0053:  newobj     instance void [System]System.ComponentModel.PropertyChangingEventHandler::.ctor(object,
                                                                                                                   native int)
              IL_0058:  ldc.i4.0
              IL_0059:  call       instance void Data.Core.Tests.Patient::SetPropertyChanging(class [mscorlib]System.Collections.IList,
                                                                                                  class [System]System.ComponentModel.PropertyChangingEventHandler,
                                                                                                  bool)
              IL_005e:  nop
              IL_005f:  br.s       IL_00ba
              IL_0061:  nop
              IL_0062:  ldarg.0
              IL_0063:  ldarg.2
              IL_0064:  callvirt   instance class [mscorlib]System.Collections.IList [Common]Common.Collections.CollectionChangingEventArgs::get_OldItems()
              IL_0069:  ldarg.0
              IL_006a:  ldftn      instance void Data.Core.Tests.Patient::OtherAddresses_PropertyChanging(object,
                                                                                                              class [System]System.ComponentModel.PropertyChangingEventArgs)
              IL_0070:  newobj     instance void [System]System.ComponentModel.PropertyChangingEventHandler::.ctor(object,
                                                                                                                   native int)
              IL_0075:  ldc.i4.0
              IL_0076:  call       instance void Data.Core.Tests.Patient::SetPropertyChanging(class [mscorlib]System.Collections.IList,
                                                                                                  class [System]System.ComponentModel.PropertyChangingEventHandler,
                                                                                                  bool)
              IL_007b:  nop
              IL_007c:  ldarg.0
              IL_007d:  ldarg.2
              IL_007e:  callvirt   instance class [mscorlib]System.Collections.IList [Common]Common.Collections.CollectionChangingEventArgs::get_NewItems()
              IL_0083:  ldarg.0
              IL_0084:  ldftn      instance void Data.Core.Tests.Patient::OtherAddresses_PropertyChanging(object,
                                                                                                              class [System]System.ComponentModel.PropertyChangingEventArgs)
              IL_008a:  newobj     instance void [System]System.ComponentModel.PropertyChangingEventHandler::.ctor(object,
                                                                                                                   native int)
              IL_008f:  ldc.i4.1
              IL_0090:  call       instance void Data.Core.Tests.Patient::SetPropertyChanging(class [mscorlib]System.Collections.IList,
                                                                                                  class [System]System.ComponentModel.PropertyChangingEventHandler,
                                                                                                  bool)
              IL_0095:  nop
              IL_0096:  br.s       IL_00ba
              IL_0098:  nop
              IL_0099:  ldarg.0
              IL_009a:  ldarg.0
              IL_009b:  ldfld      class [Common]Common.Collections.Flock`1<class Data.Core.Tests.IAddress> Data.Core.Tests.Patient::_otherAddresses
              IL_00a0:  call       class [mscorlib]System.Collections.Generic.List`1<!!0> [System.Core]System.Linq.Enumerable::ToList<class Data.Core.Tests.IAddress>(class [mscorlib]System.Collections.Generic.IEnumerable`1<!!0>)
              IL_00a5:  ldarg.0
              IL_00a6:  ldftn      instance void Data.Core.Tests.Patient::OtherAddresses_PropertyChanging(object,
                                                                                                              class [System]System.ComponentModel.PropertyChangingEventArgs)
              IL_00ac:  newobj     instance void [System]System.ComponentModel.PropertyChangingEventHandler::.ctor(object,
                                                                                                                   native int)
              IL_00b1:  ldc.i4.0
              IL_00b2:  call       instance void Data.Core.Tests.Patient::SetPropertyChanging(class [mscorlib]System.Collections.IList,
                                                                                                  class [System]System.ComponentModel.PropertyChangingEventHandler,
                                                                                                  bool)
              IL_00b7:  nop
              IL_00b8:  br.s       IL_00ba
              IL_00ba:  ldarg.0
              IL_00bb:  ldstr      "OtherAddresses"
              IL_00c0:  callvirt   instance void class [Data.Core]Data.Core.Dynamic.Extendable`1<class [UX.Core]UX.Core.ViewModelBase>::OnPropertyChanging(string)
              IL_00c5:  nop
              IL_00c6:  ret
            } // end of method Patient::OtherAddresses_CollectionChanging
            */
            var itemPropertyChanging = BuildCollectionItemPropertyChangingHandler(interfaceType, typeBuilder, baseType, fld, propertyChanging);
            var method = typeBuilder.DefineMethod(fld.Name + "_CollectionChanging", MethodAttributes.Private | MethodAttributes.HideBySig,
                typeof(void),
                new Type[] { typeof(object), typeof(CollectionChangingEventArgs) });

            var code = method.GetILGenerator();
            var v_0 = code.DeclareLocal(typeof(NotifyCollectionChangedAction));
            var endOfSwitch = code.DefineLabel();
            code.Emit(OpCodes.Ldarg_2);
            code.Emit(OpCodes.Callvirt, typeof(CollectionChangingEventArgs).GetProperty("Action").GetMethod);
            code.Emit(OpCodes.Stloc, v_0);
            code.Emit(OpCodes.Ldloc, v_0);
            var jumpTable = new Label[]
            {
                code.DefineLabel(), // add
                code.DefineLabel(), // remove
                code.DefineLabel(), // replace
                endOfSwitch, // move
                code.DefineLabel()  // reset
            };
            code.Emit(OpCodes.Switch, jumpTable);

            code.Emit(OpCodes.Br, endOfSwitch);
            // case System.Collections.Specialized.NotifyCollectionChangedAction.Add:
            // {
            //     SetPropertyChanging(e.NewItems, OtherAddresses_PropertyChanging, true);
            //     break;
            // }
            code.MarkLabel(jumpTable[0]);
            code.Emit(OpCodes.Ldarg_0);
            code.Emit(OpCodes.Ldarg_2);
            code.Emit(OpCodes.Callvirt, typeof(CollectionChangingEventArgs).GetProperty("NewItems").GetMethod);
            code.Emit(OpCodes.Ldarg_0);
            code.Emit(OpCodes.Ldftn, itemPropertyChanging);
            code.Emit(OpCodes.Newobj, typeof(PropertyChangingEventHandler).GetConstructor(new Type[] { typeof(object), typeof(IntPtr) }));
            code.Emit(OpCodes.Ldc_I4_1);
            code.Emit(OpCodes.Call, setPropertyChanging);
            code.Emit(OpCodes.Br_S, endOfSwitch);

            // case System.Collections.Specialized.NotifyCollectionChangedAction.Remove:
            // {
            //     SetPropertyChanging(e.OldItems, OtherAddresses_PropertyChanging, false);
            //     break;
            // }
            code.MarkLabel(jumpTable[1]);
            code.Emit(OpCodes.Ldarg_0);
            code.Emit(OpCodes.Ldarg_2);
            code.Emit(OpCodes.Callvirt, typeof(CollectionChangingEventArgs).GetProperty("OldItems").GetMethod);
            code.Emit(OpCodes.Ldarg_0);
            code.Emit(OpCodes.Ldftn, itemPropertyChanging);
            code.Emit(OpCodes.Newobj, typeof(PropertyChangingEventHandler).GetConstructor(new Type[] { typeof(object), typeof(IntPtr) }));
            code.Emit(OpCodes.Ldc_I4_0);
            code.Emit(OpCodes.Call, setPropertyChanging);
            code.Emit(OpCodes.Br_S, endOfSwitch);

            // case System.Collections.Specialized.NotifyCollectionChangedAction.Replace:
            //             {
            //     SetPropertyChanging(e.OldItems, OtherAddresses_PropertyChanging, false);
            //     SetPropertyChanging(e.NewItems, OtherAddresses_PropertyChanging, true);
            //     break;
            // }
            code.MarkLabel(jumpTable[2]);
            code.Emit(OpCodes.Ldarg_0);
            code.Emit(OpCodes.Ldarg_2);
            code.Emit(OpCodes.Callvirt, typeof(CollectionChangingEventArgs).GetProperty("OldItems").GetMethod);
            code.Emit(OpCodes.Ldarg_0);
            code.Emit(OpCodes.Ldftn, itemPropertyChanging);
            code.Emit(OpCodes.Newobj, typeof(PropertyChangingEventHandler).GetConstructor(new Type[] { typeof(object), typeof(IntPtr) }));
            code.Emit(OpCodes.Ldc_I4_0);
            code.Emit(OpCodes.Call, setPropertyChanging);
            code.Emit(OpCodes.Ldarg_0);
            code.Emit(OpCodes.Ldarg_2);
            code.Emit(OpCodes.Callvirt, typeof(CollectionChangingEventArgs).GetProperty("NewItems").GetMethod);
            code.Emit(OpCodes.Ldarg_0);
            code.Emit(OpCodes.Ldftn, itemPropertyChanging);
            code.Emit(OpCodes.Newobj, typeof(PropertyChangingEventHandler).GetConstructor(new Type[] { typeof(object), typeof(IntPtr) }));
            code.Emit(OpCodes.Ldc_I4_1);
            code.Emit(OpCodes.Call, setPropertyChanging);
            code.Emit(OpCodes.Br_S, endOfSwitch);

            // case System.Collections.Specialized.NotifyCollectionChangedAction.Reset:
            //             {
            //     SetPropertyChanging(_otherAddresses.ToList(), OtherAddresses_PropertyChanging, false);
            //     break;
            // }
            code.MarkLabel(jumpTable[4]);
            code.Emit(OpCodes.Ldarg_0);
            code.Emit(OpCodes.Ldarg_0);
            code.Emit(OpCodes.Ldfld, fld);
            code.Emit(OpCodes.Call, typeof(Enumerable).GetMethod("ToList", BindingFlags.Public | BindingFlags.Static).MakeGenericMethod(fld.FieldType.GetGenericArguments()[0]));
            code.Emit(OpCodes.Ldarg_0);
            code.Emit(OpCodes.Ldftn, itemPropertyChanging);
            code.Emit(OpCodes.Newobj, typeof(PropertyChangingEventHandler).GetConstructor(new Type[] { typeof(object), typeof(IntPtr) }));
            code.Emit(OpCodes.Ldc_I4_0);
            code.Emit(OpCodes.Call, setPropertyChanging);
            code.Emit(OpCodes.Br_S, endOfSwitch);


            code.MarkLabel(endOfSwitch);

            MethodInfo onPropertyChangingMI = null;
            if (baseType != null && baseType.FindInterfaces(
                (type, filter) => type.FullName.Contains(filter.ToString()),
                typeof(INotifyPropertyChanging).FullName).Any())
            {
                onPropertyChangingMI = baseType.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                    .SingleOrDefault(mi =>
                           mi.Name.Equals("OnPropertyChanging")
                        && mi.GetParameters().Length == 1
                        && mi.GetParameters()[0].ParameterType.Equals(typeof(string)));
            }

            if (onPropertyChangingMI == null)
            {
                // implement the ProperyChanged event raise
                var noSubs = code.DefineLabel();
                code.Emit(OpCodes.Ldarg_0);
                code.Emit(OpCodes.Ldfld, propertyChanging);
                code.Emit(OpCodes.Ldnull);
                code.Emit(OpCodes.Cgt_Un);
                code.Emit(OpCodes.Brfalse_S, noSubs);
                code.Emit(OpCodes.Ldarg_0);
                BuildFormattedCollectionChangingPropertyString(code, fld.Name.Substring(1));
                code.Emit(OpCodes.Newobj, typeof(PropertyChangedEventArgs).GetConstructor(new[] { typeof(string) }));
                code.Emit(OpCodes.Callvirt, typeof(PropertyChangedEventHandler).GetMethod("Invoke", BindingFlags.Public | BindingFlags.Instance));
                code.Emit(OpCodes.Ldarg_0);
                code.Emit(OpCodes.Ldstr, fld.Name.Substring(1));
                code.Emit(OpCodes.Newobj, typeof(PropertyChangingEventArgs).GetConstructor(new[] { typeof(string) }));
                code.Emit(OpCodes.Callvirt, typeof(PropertyChangingEventHandler).GetMethod("Invoke", BindingFlags.Public | BindingFlags.Instance));
                code.MarkLabel(noSubs);
            }
            else
            {
                code.Emit(OpCodes.Ldarg_0);
                BuildFormattedCollectionChangingPropertyString(code, fld.Name.Substring(1));
                code.Emit(OpCodes.Callvirt, onPropertyChangingMI);
                code.Emit(OpCodes.Ldarg_0);
                code.Emit(OpCodes.Ldstr, fld.Name.Substring(1));
                code.Emit(OpCodes.Callvirt, onPropertyChangingMI);
            }

            code.Emit(OpCodes.Ret);
            
            return method;
        }

        protected static MethodInfo BuildCollectionItemPropertyChangingHandler(Type interfaceType, TypeBuilder typeBuilder, Type baseType, FieldInfo field, FieldInfo propertyChanging)
        {
            /* 
            .method private hidebysig instance void  OtherAddresses_Item_PropertyChanging(object sender,
                                                                         class [System]System.ComponentModel.PropertyChangingEventArgs e) cil managed
            {
              // Code size       25 (0x19)
              .maxstack  8
              IL_0000:  nop
              IL_0001:  ldarg.0
              IL_0002:  ldstr      "OtherAddresses.Item."
              IL_0007:  ldarg.2
              IL_0008:  callvirt   instance string [System]System.ComponentModel.PropertyChangingEventArgs::get_PropertyName()
              IL_000d:  call       string [mscorlib]System.String::Concat(string,
                                                                          string)
              IL_0012:  callvirt   instance void class [Data.Core]Data.Core.Dynamic.Extendable`1<class [UX.Core]UX.Core.ViewModelBase>::OnPropertyChanging(string)
              IL_0017:  nop
              IL_0018:  ret
            } // end of method Patient::OtherAddresses_PropertyChanging
            */
            var name = field.Name + "_Item_PropertyChanging";
            var method = typeBuilder.DefineMethod(name, 
                MethodAttributes.Private | MethodAttributes.HideBySig, 
                typeof(void), new Type[] { typeof(object), typeof(PropertyChangingEventArgs) });

            var code = method.GetILGenerator();
            

            MethodInfo onPropertyChangingMI = null;
            if (baseType != null && baseType.FindInterfaces(
                (type, filter) => type.FullName.Contains(filter.ToString()),
                typeof(INotifyPropertyChanging).FullName).Any())
            {
                onPropertyChangingMI = baseType.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                    .SingleOrDefault(mi =>
                           mi.Name.Equals("OnPropertyChanging")
                        && mi.GetParameters().Length == 1
                        && mi.GetParameters()[0].ParameterType.Equals(typeof(string)));
            }

            if (onPropertyChangingMI == null)
            {
                // implement the ProperyChanged event raise
                var noSubs = code.DefineLabel();
                code.Emit(OpCodes.Ldarg_0);
                code.Emit(OpCodes.Ldfld, propertyChanging);
                code.Emit(OpCodes.Ldnull);
                code.Emit(OpCodes.Cgt_Un);
                code.Emit(OpCodes.Brfalse_S, noSubs);
                code.Emit(OpCodes.Ldarg_0);
                code.Emit(OpCodes.Ldstr, field.Name.Substring(1) + ".Item.");
                code.Emit(OpCodes.Ldarg_2);
                code.Emit(OpCodes.Callvirt, typeof(PropertyChangingEventArgs).GetProperty("PropertyName").GetMethod);
                code.Emit(OpCodes.Call, typeof(string).GetMethod("Concat", BindingFlags.Public | BindingFlags.Static, null, new Type[] { typeof(string), typeof(string) }, null));
                code.Emit(OpCodes.Newobj, typeof(PropertyChangingEventArgs).GetConstructor(new[] { typeof(string) }));
                code.Emit(OpCodes.Callvirt, typeof(PropertyChangingEventHandler).GetMethod("Invoke", BindingFlags.Public | BindingFlags.Instance));
                code.MarkLabel(noSubs);
            }
            else
            {
                code.Emit(OpCodes.Ldarg_0);
                code.Emit(OpCodes.Ldstr, field.Name.Substring(1) + ".Item.");
                code.Emit(OpCodes.Ldarg_2);
                code.Emit(OpCodes.Callvirt, typeof(PropertyChangingEventArgs).GetProperty("PropertyName").GetMethod);
                code.Emit(OpCodes.Call, typeof(string).GetMethod("Concat", BindingFlags.Public | BindingFlags.Static, null, new Type[] { typeof(string), typeof(string) }, null));
                code.Emit(OpCodes.Callvirt, onPropertyChangingMI);
            }
            code.Emit(OpCodes.Ret);

            return method;
        }

        protected static MethodInfo BuildCollectionItemPropertyChangedHandler(Type interfaceType, TypeBuilder typeBuilder, Type baseType, FieldInfo field, FieldInfo propertyChanged)
        {
            /* 
            .method private hidebysig instance void  OtherAddresses_Item_PropertyChanging(object sender,
                                                                         class [System]System.ComponentModel.PropertyChangingEventArgs e) cil managed
            {
              // Code size       25 (0x19)
              .maxstack  8
              IL_0000:  nop
              IL_0001:  ldarg.0
              IL_0002:  ldstr      "OtherAddresses.Item."
              IL_0007:  ldarg.2
              IL_0008:  callvirt   instance string [System]System.ComponentModel.PropertyChangingEventArgs::get_PropertyName()
              IL_000d:  call       string [mscorlib]System.String::Concat(string,
                                                                          string)
              IL_0012:  callvirt   instance void class [Data.Core]Data.Core.Dynamic.Extendable`1<class [UX.Core]UX.Core.ViewModelBase>::OnPropertyChanging(string)
              IL_0017:  nop
              IL_0018:  ret
            } // end of method Patient::OtherAddresses_PropertyChanging
            */
            var name = field.Name + "_Item_PropertyChanged";
            var method = typeBuilder.DefineMethod(name,
                MethodAttributes.Private | MethodAttributes.HideBySig,
                typeof(void), new Type[] { typeof(object), typeof(PropertyChangingEventArgs) });

            var code = method.GetILGenerator();


            MethodInfo onPropertyChangedMI = null;
            if (baseType != null && baseType.FindInterfaces(
                (type, filter) => type.FullName.Contains(filter.ToString()),
                typeof(INotifyPropertyChanged).FullName).Any())
            {
                onPropertyChangedMI = baseType.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                    .SingleOrDefault(mi =>
                           mi.Name.Equals("OnPropertyChanged")
                        && mi.GetParameters().Length == 1
                        && mi.GetParameters()[0].ParameterType.Equals(typeof(string)));
            }

            if (onPropertyChangedMI == null)
            {
                // implement the ProperyChanged event raise
                var noSubs = code.DefineLabel();
                code.Emit(OpCodes.Ldarg_0);
                code.Emit(OpCodes.Ldfld, propertyChanged);
                code.Emit(OpCodes.Ldnull);
                code.Emit(OpCodes.Cgt_Un);
                code.Emit(OpCodes.Brfalse_S, noSubs);
                code.Emit(OpCodes.Ldarg_0);
                code.Emit(OpCodes.Ldstr, field.Name.Substring(1) + ".Item.");
                code.Emit(OpCodes.Ldarg_2);
                code.Emit(OpCodes.Callvirt, typeof(PropertyChangedEventArgs).GetProperty("PropertyName").GetMethod);
                code.Emit(OpCodes.Call, typeof(string).GetMethod("Concat", BindingFlags.Public | BindingFlags.Static, null, new Type[] { typeof(string), typeof(string) }, null));
                code.Emit(OpCodes.Newobj, typeof(PropertyChangedEventArgs).GetConstructor(new[] { typeof(string) }));
                code.Emit(OpCodes.Callvirt, typeof(PropertyChangedEventHandler).GetMethod("Invoke", BindingFlags.Public | BindingFlags.Instance));
                code.MarkLabel(noSubs);
            }
            else
            {
                code.Emit(OpCodes.Ldarg_0);
                code.Emit(OpCodes.Ldstr, field.Name.Substring(1) + ".Item.");
                code.Emit(OpCodes.Ldarg_2);
                code.Emit(OpCodes.Callvirt, typeof(PropertyChangedEventArgs).GetProperty("PropertyName").GetMethod);
                code.Emit(OpCodes.Call, typeof(string).GetMethod("Concat", BindingFlags.Public | BindingFlags.Static, null, new Type[] { typeof(string), typeof(string) }, null));
                code.Emit(OpCodes.Callvirt, onPropertyChangedMI);
            }
            code.Emit(OpCodes.Ret);

            return method;
        }

        protected static ConstructorInfo BuildDictionaryCtor(Type interfaceType, TypeBuilder typeBuilder, ConstructorInfo defaultCtor, PropertyInfo[] props)
        {
            var ctor = typeBuilder.DefineConstructor(MethodAttributes.Public
                | MethodAttributes.HideBySig
                | MethodAttributes.SpecialName
                | MethodAttributes.RTSpecialName,
                CallingConventions.Standard,
                new Type[] { typeof(Dictionary<string, object>) });

            var ctorCode = ctor.GetILGenerator();

            // call default ctor
            ctorCode.Emit(OpCodes.Ldarg_0);
            ctorCode.Emit(OpCodes.Call, defaultCtor);

            var converter = ctorCode.DeclareLocal(typeof(TypeConverter));
            foreach (var prop in props.Where(p => p.CanWrite))
            {
                var local = ctorCode.DeclareLocal(typeof(object));
                var converted = ctorCode.DeclareLocal(prop.PropertyType);

                // if (dictionary.TryGetValue("Property", out local)
                ctorCode.Emit(OpCodes.Ldarg_1); // load the dictionary
                ctorCode.Emit(OpCodes.Ldstr, prop.Name.Equals("Owner") ? "_owner" : prop.Name);
                ctorCode.Emit(OpCodes.Ldloca_S, local); // load as an OUT parameter
                ctorCode.Emit(OpCodes.Callvirt, typeof(Dictionary<string, object>).GetMethod("TryGetValue", BindingFlags.Public | BindingFlags.Instance));
                var endOfAssignment = ctorCode.DefineLabel();
                ctorCode.Emit(OpCodes.Brfalse_S, endOfAssignment);
                ctorCode.Emit(OpCodes.Ldloc, local); // property value in dictionary
                if (prop.PropertyType.IsArray && prop.PropertyType.ResolveElementType().Implements<IModel>())
                {
                    var locArray = ctorCode.DeclareLocal(prop.PropertyType);
                    //T[] Model.New<ITestSub>((IList<Dictionary<string, object>>)obj2, null);
                    ctorCode.Emit(OpCodes.Castclass, typeof(IList<object>));
                    ctorCode.Emit(OpCodes.Ldnull);
                    ctorCode.Emit(OpCodes.Call, typeof(Model).GetMethods(BindingFlags.Public | BindingFlags.Static)
                        .Single(mi => mi.GetParameters().Length == 2 && mi.ReturnType.IsArray && mi.Name.Equals("NewArray")).MakeGenericMethod(prop.PropertyType.ResolveElementType()));
                    ctorCode.Emit(OpCodes.Stloc, locArray);
                    ctorCode.Emit(OpCodes.Ldarg_0);
                    ctorCode.Emit(OpCodes.Ldloc, locArray);
                    ctorCode.Emit(OpCodes.Call, prop.SetMethod);
                }
                else if (prop.PropertyType.Implements<IList>() && prop.PropertyType.ResolveElementType().Implements<IModel>())
                {
                    // Model.New<ITestSub>((IList<Dictionary<string, object>>)obj2, Items, null);
                    ctorCode.Emit(OpCodes.Castclass, typeof(IList<object>));
                    ctorCode.Emit(OpCodes.Ldarg_0);
                    ctorCode.Emit(OpCodes.Call, prop.GetMethod);
                    ctorCode.Emit(OpCodes.Ldnull);
                    ctorCode.Emit(OpCodes.Call, typeof(Model).GetMethods(BindingFlags.Public | BindingFlags.Static)
                        .Single(mi => mi.GetParameters().Length == 3 && mi.Name.Equals("New")).MakeGenericMethod(prop.PropertyType.ResolveElementType()));
                }
                else
                {
                    //      if (local is property.PropertyType)
                    ctorCode.Emit(OpCodes.Isinst, prop.PropertyType); // is dictionary value type already correct type?
                    ctorCode.Emit(OpCodes.Ldnull);
                    ctorCode.Emit(OpCodes.Cgt_Un);
                    var typeNeedsConversion = ctorCode.DefineLabel();
                    ctorCode.Emit(OpCodes.Brfalse_S, typeNeedsConversion);
                    //      this.Property = (property.Type)local
                    ctorCode.Emit(OpCodes.Ldarg_0);
                    ctorCode.Emit(OpCodes.Ldloc, local);
                    ctorCode.Emit(OpCodes.Unbox_Any, prop.PropertyType); // unbox to native type
                    ctorCode.Emit(OpCodes.Call, prop.SetMethod); // set the value
                    ctorCode.Emit(OpCodes.Br_S, endOfAssignment); // end the assignment

                    // we need to convert the dictionary type, if possible, to the property type
                    ctorCode.MarkLabel(typeNeedsConversion);
                    // else 
                    // {
                    //      if (local.TryCast<property.Type>(out converted))
                    //      {
                    //          this.Property = converted
                    //      }
                    ctorCode.Emit(OpCodes.Ldloc, local);
                    ctorCode.Emit(OpCodes.Ldloca, converted);
                    ctorCode.Emit(OpCodes.Call, typeof(ValueTypesEx).GetMethods(BindingFlags.Public | BindingFlags.Static)
                                                                    .Single(mi => mi.Name.Equals("TryCast") && mi.IsGenericMethod)
                                                                    .MakeGenericMethod(prop.PropertyType));
                    var cantTryCast = ctorCode.DefineLabel();
                    ctorCode.Emit(OpCodes.Brfalse_S, cantTryCast);
                    ctorCode.Emit(OpCodes.Ldarg_0);
                    ctorCode.Emit(OpCodes.Ldloc, converted);
                    ctorCode.Emit(OpCodes.Call, prop.SetMethod);
                    ctorCode.Emit(OpCodes.Br_S, endOfAssignment);
                    // we have a value, now see if can be converted
                    //      else
                    //      {
                    //          converter = TypeDescriptor.GetConverter(local.GetType())
                    ctorCode.MarkLabel(cantTryCast);
                    ctorCode.Emit(OpCodes.Ldtoken, prop.PropertyType);
                    ctorCode.Emit(OpCodes.Call, typeof(Type).GetMethod("GetTypeFromHandle", BindingFlags.Public | BindingFlags.Static));
                    ctorCode.Emit(OpCodes.Call, typeof(TypeDescriptor).GetMethod("GetConverter", BindingFlags.Public | BindingFlags.Static, null, new Type[] { typeof(Type) }, null));
                    ctorCode.Emit(OpCodes.Stloc, converter);
                    // set the target type for custom type converter on embedded model types
                    //          ModelTypeConverter.TargetType = typeof(local.Type)
                    ctorCode.Emit(OpCodes.Ldtoken, prop.PropertyType);
                    ctorCode.Emit(OpCodes.Call, typeof(Type).GetMethod("GetTypeFromHandle", BindingFlags.Public | BindingFlags.Static));
                    ctorCode.Emit(OpCodes.Call, typeof(ModelTypeConverter).GetProperty("TargetType", BindingFlags.Public | BindingFlags.Static).GetSetMethod());
                    // check if we can convert from the source value
                    //          if (converter.CanConvertTo(typeof(local.Type))
                    ctorCode.Emit(OpCodes.Ldloc, converter);
                    ctorCode.Emit(OpCodes.Ldloc, local);
                    ctorCode.Emit(OpCodes.Callvirt, typeof(object).GetMethod("GetType", BindingFlags.Public | BindingFlags.Instance));
                    ctorCode.Emit(OpCodes.Callvirt, typeof(TypeConverter).GetMethod("CanConvertFrom", BindingFlags.Public | BindingFlags.Instance, null, new Type[] { typeof(Type) }, null));
                    ctorCode.Emit(OpCodes.Brfalse_S, endOfAssignment); // cant convert, so bail out
                                                                       // we can convert, so convert and assign
                                                                       //          this.Property = converter.ConvertTo(local, property.Type)
                    ctorCode.Emit(OpCodes.Ldarg_0);
                    ctorCode.Emit(OpCodes.Ldloc, converter);
                    ctorCode.Emit(OpCodes.Ldloc, local);
                    ctorCode.Emit(OpCodes.Callvirt, typeof(TypeConverter).GetMethod("ConvertFrom", BindingFlags.Public | BindingFlags.Instance, null, new Type[] { typeof(object) }, null));
                    ctorCode.Emit(OpCodes.Unbox_Any, prop.PropertyType); // unbox to native type
                    ctorCode.Emit(OpCodes.Call, prop.SetMethod); // set the value
                }
                ctorCode.MarkLabel(endOfAssignment); // end of property block
            }

            ctorCode.Emit(OpCodes.Ret);
            return ctor;
        }

        protected static PropertyInfo BuildModelTypeProperty(Type interfaceType, TypeBuilder typeBuilder, Type baseType, PropertyInfo member, FieldInfo propertyChanged, FieldInfo propertyChanging)
        {
            var property = typeBuilder.DefineProperty(member.Name,
                PropertyAttributes.HasDefault,
                member.PropertyType,
                null);
            var getter = typeBuilder.DefineMethod("get_" + member.Name,
                MethodAttributes.Public
                | MethodAttributes.SpecialName
                | MethodAttributes.HideBySig
                | MethodAttributes.NewSlot
                | MethodAttributes.Final
                | MethodAttributes.Virtual,
                member.PropertyType,
                Type.EmptyTypes);
            getter.SetCustomAttribute(new CustomAttributeBuilder(typeof(JsonIgnoreAttribute).GetConstructor(Type.EmptyTypes), new object[0]));

            var getterCode = getter.GetILGenerator();

            getterCode.Emit(OpCodes.Ldtoken, interfaceType);
            getterCode.Emit(OpCodes.Call, typeof(Type).GetMethod("GetTypeFromHandle", BindingFlags.Public | BindingFlags.Static));
            getterCode.Emit(OpCodes.Ret);

            property.SetGetMethod(getter);
            var baseMethod = member.GetMethod;
            var abstractMethod = baseType.GetProperty(member.Name, BindingFlags.Public | BindingFlags.Instance);

            if (abstractMethod != null && (abstractMethod.GetMethod.IsAbstract || abstractMethod.GetMethod.IsVirtual))
            {
                baseMethod = abstractMethod.GetMethod;
            }
            
            typeBuilder.DefineMethodOverride(getter, baseMethod);
            return property;
        }

        protected static PropertyInfo GetExplicitProperty(Type interfaceType, Type baseType, PropertyInfo member)
        {
            foreach(var iface in interfaceType.GetInterfaces())
            {
                var explicitName = string.Format("{0}.{1}", GetExplicitName(iface), member.Name);
                try
                {
                    var prop = iface.GetProperty(member.Name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    if (prop != null && prop.PropertyType.Equals(member.PropertyType))
                        return prop;
                }
                catch { }
            }
            return null;
        }

        protected static string GetExplicitName(Type iface)
        {
            if (iface.IsGenericType)
            {
                var genArgs = iface.GetGenericArguments();
                var sb = new StringBuilder();
                sb.Append(iface.Name.Split('`')[0]);
                sb.Append("<");
                var isFirst = true;
                foreach(var genArg in genArgs)
                {
                    if (!isFirst)
                        sb.Append(",");
                    sb.Append(genArg.Name);
                    isFirst = false;
                }
                sb.Append(">");
                return sb.ToString();
            }
            else
            {
                return iface.Name;
            }
        }

        protected static PropertyInfo BuildProperty(Type interfaceType, TypeBuilder typeBuilder, Type baseType, PropertyInfo member, FieldInfo propertyChanged, FieldInfo propertyChanging, out FieldInfo fld)
        {
            MethodInfo modelPropertyChanged = null;
            MethodInfo modelPropertyChanging = null;

            if (member.PropertyType.Implements<IModel>())
            {
                modelPropertyChanged = BuildForwardedPropertyChangedHandler(interfaceType, typeBuilder, baseType, member, propertyChanged);
                modelPropertyChanging = BuildForwardedPropertyChangingHandler(interfaceType, typeBuilder, baseType, member, propertyChanging);
            }

            var baseTypeProp = baseType == null ? null : baseType.GetProperty(member.Name, BindingFlags.Public | BindingFlags.Instance);

            if (baseTypeProp != null)
            {
                if ((member.CanRead && !baseTypeProp.CanRead)
                    || (member.CanWrite && !baseTypeProp.CanWrite))
                {
                    // we only map to the base type property if both setter and getter match the 
                    baseTypeProp = null;
                }
                else if(!member.PropertyType.Equals(baseTypeProp.PropertyType))
                {
                    // we need to check to see if we have an explicit interface implementation that this property is hiding 
                    // if its not found, then we set to null
                    baseTypeProp = GetExplicitProperty(interfaceType, baseType, member);
                }
            }

            fld = null;
            if (baseTypeProp == null)
            {
                // need to emit a backing field for the value
                fld = typeBuilder.DefineField("_" + member.Name, member.PropertyType, FieldAttributes.Private);
            }

            var property = typeBuilder.DefineProperty(member.Name, //baseTypeProp == null ? member.Name : member.DeclaringType.FullName + "." + member.Name,
                PropertyAttributes.HasDefault,
                member.PropertyType,
                null);
            if (property.PropertyType.Implements<IEnumerable>() && property.PropertyType.ResolveElementType().Implements<IModel>())
            {
                property.SetCustomAttribute(new CustomAttributeBuilder(typeof(BinarySerializableAttribute).GetConstructors()
                    .Single(c => c.GetParameters().Length == 2), new object[] { _propertyNumber, property.PropertyType.ResolveElementType().MakeArrayType()}));
            }
            else
            {
                property.SetCustomAttribute(new CustomAttributeBuilder(typeof(BinarySerializableAttribute).GetConstructors()
                    .Single(c => c.GetParameters().Length == 1), new object[] { _propertyNumber }));
            }

            if (member.CanRead)
            {
                var getter = typeBuilder.DefineMethod("get_" + member.Name,
                MethodAttributes.Public
                | MethodAttributes.SpecialName
                | MethodAttributes.HideBySig
                | MethodAttributes.NewSlot
                | MethodAttributes.Final
                | MethodAttributes.Virtual,
                member.PropertyType,
                Type.EmptyTypes);
                var getterCode = getter.GetILGenerator();
                if (baseTypeProp == null)
                {
                    // base type does not define this 
                    getterCode.Emit(OpCodes.Ldarg_0);
                    getterCode.Emit(OpCodes.Ldfld, fld);
                    if (!member.PropertyType.IsInterface && member.PropertyType.Implements<IList>() && !member.PropertyType.IsArray)
                    {
                        // new up and empty instance if the backing field is null
                        getterCode.Emit(OpCodes.Ldnull);
                        getterCode.Emit(OpCodes.Ceq);
                        var isNotNull = getterCode.DefineLabel();
                        getterCode.Emit(OpCodes.Brfalse_S, isNotNull);
                        getterCode.Emit(OpCodes.Ldarg_0);
                        getterCode.Emit(OpCodes.Newobj, member.PropertyType.GetConstructor(Type.EmptyTypes));
                        getterCode.Emit(OpCodes.Stfld, fld);
                        getterCode.MarkLabel(isNotNull);
                        getterCode.Emit(OpCodes.Ldarg_0);
                        getterCode.Emit(OpCodes.Ldfld, fld);
                    }
                    getterCode.Emit(OpCodes.Ret);
                }
                else
                {
                    // use basetype to define the property
                    getterCode.Emit(OpCodes.Ldarg_0);
                    getterCode.Emit(OpCodes.Call, baseTypeProp.GetMethod);
                    getterCode.Emit(OpCodes.Ret);
                }
                property.SetGetMethod(getter);
                typeBuilder.DefineMethodOverride(getter, member.GetMethod);
            }

            if (member.CanWrite)
            {
                var setter = typeBuilder.DefineMethod("set_" + member.Name,
                    MethodAttributes.Public
                    | MethodAttributes.SpecialName
                    | MethodAttributes.HideBySig
                    | MethodAttributes.NewSlot
                    | MethodAttributes.Final
                    | MethodAttributes.Virtual,
                    null,
                    new[] { member.PropertyType });
                var setterCode = setter.GetILGenerator();
                var returnLabel = setterCode.DefineLabel();
                var isNotifyPropertyChanged = interfaceType.FindInterfaces(
                    (type, filter) => type.FullName.Contains(filter.ToString()),
                    typeof(INotifyPropertyChanged).FullName).Any();
                var isNotifyPropertyChanging = interfaceType.FindInterfaces(
                    (type, filter) => type.FullName.Contains(filter.ToString()),
                    typeof(INotifyPropertyChanging).FullName).Any();
                
                // remove the event handler before changing the embedded model instance to a new instance
                if (isNotifyPropertyChanged && modelPropertyChanged != null)
                {
                    
                    setterCode.Emit(OpCodes.Ldarg_0);
                    if (baseTypeProp == null)
                    {
                        setterCode.Emit(OpCodes.Ldfld, fld);
                    }
                    else
                    {
                        setterCode.Emit(OpCodes.Callvirt, baseTypeProp.GetMethod);
                    }
                    setterCode.Emit(OpCodes.Ldnull);
                    setterCode.Emit(OpCodes.Cgt_Un);
                    var subModelIsNull = setterCode.DefineLabel();
                    setterCode.Emit(OpCodes.Brfalse_S, subModelIsNull);
                    setterCode.Emit(OpCodes.Ldarg_0);
                    if (baseTypeProp == null)
                    {
                        setterCode.Emit(OpCodes.Ldfld, fld);
                    }
                    else
                    {
                        setterCode.Emit(OpCodes.Callvirt, baseTypeProp.GetMethod);
                    }
                    setterCode.Emit(OpCodes.Ldarg_0);
                    setterCode.Emit(OpCodes.Ldftn, modelPropertyChanged);
                    setterCode.Emit(OpCodes.Newobj, typeof(PropertyChangedEventHandler).GetConstructor(new Type[] { typeof(object), typeof(IntPtr) }));
                    setterCode.Emit(OpCodes.Callvirt, typeof(INotifyPropertyChanged).GetEvent("PropertyChanged").RemoveMethod);
                    setterCode.MarkLabel(subModelIsNull);
                }

                // remove the event handler before changing the embedded model instance to a new instance
                if (isNotifyPropertyChanging && modelPropertyChanging != null)
                {

                    setterCode.Emit(OpCodes.Ldarg_0);
                    if (baseTypeProp == null)
                    {
                        setterCode.Emit(OpCodes.Ldfld, fld);
                    }
                    else
                    {
                        setterCode.Emit(OpCodes.Callvirt, baseTypeProp.GetMethod);
                    }
                    setterCode.Emit(OpCodes.Ldnull);
                    setterCode.Emit(OpCodes.Cgt_Un);
                    var subModelIsNull = setterCode.DefineLabel();
                    setterCode.Emit(OpCodes.Brfalse_S, subModelIsNull);
                    setterCode.Emit(OpCodes.Ldarg_0);
                    if (baseTypeProp == null)
                    {
                        setterCode.Emit(OpCodes.Ldfld, fld);
                    }
                    else
                    {
                        setterCode.Emit(OpCodes.Callvirt, baseTypeProp.GetMethod);
                    }
                    setterCode.Emit(OpCodes.Ldarg_0);
                    setterCode.Emit(OpCodes.Ldftn, modelPropertyChanging);
                    setterCode.Emit(OpCodes.Newobj, typeof(PropertyChangingEventHandler).GetConstructor(new Type[] { typeof(object), typeof(IntPtr) }));
                    setterCode.Emit(OpCodes.Callvirt, typeof(INotifyPropertyChanging).GetEvent("PropertyChanging").RemoveMethod);
                    setterCode.MarkLabel(subModelIsNull);
                }

                // raise the property changing event
                if (isNotifyPropertyChanging)
                {
                    MethodInfo onPropertyChangingMI = null;
                    if (baseType != null && baseType.FindInterfaces(
                        (type, filter) => type.FullName.Contains(filter.ToString()),
                        typeof(INotifyPropertyChanging).FullName).Any())
                    {
                        onPropertyChangingMI = baseType.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                            .SingleOrDefault(mi =>
                                   mi.Name.Equals("OnPropertyChanging")
                                && mi.GetParameters().Length == 1
                                && mi.GetParameters()[0].ParameterType.Equals(typeof(string)));
                    }

                    if (onPropertyChangingMI == null)
                    {
                        // implement the ProperyChanged event raise
                        var noSubs = setterCode.DefineLabel();
                        setterCode.Emit(OpCodes.Ldarg_0);
                        setterCode.Emit(OpCodes.Ldfld, propertyChanging);
                        setterCode.Emit(OpCodes.Ldnull);
                        setterCode.Emit(OpCodes.Cgt_Un);
                        setterCode.Emit(OpCodes.Brfalse_S, noSubs);
                        setterCode.Emit(OpCodes.Ldarg_0);
                        setterCode.Emit(OpCodes.Ldfld, propertyChanging);
                        setterCode.Emit(OpCodes.Ldarg_0);
                        setterCode.Emit(OpCodes.Ldstr, member.Name);
                        setterCode.Emit(OpCodes.Newobj, typeof(PropertyChangingEventArgs).GetConstructor(new[] { typeof(string) }));
                        setterCode.Emit(OpCodes.Callvirt, typeof(PropertyChangingEventHandler).GetMethod("Invoke", BindingFlags.Public | BindingFlags.Instance));
                        setterCode.MarkLabel(noSubs);
                    }
                    else
                    {
                        setterCode.Emit(OpCodes.Ldarg_0);
                        setterCode.Emit(OpCodes.Ldstr, member.Name);
                        setterCode.Emit(OpCodes.Callvirt, onPropertyChangingMI);
                    }
                }
                
                // assign the new value
                if (baseTypeProp == null)
                {
                    // base type does not define this 
                    setterCode.Emit(OpCodes.Ldarg_0);
                    setterCode.Emit(OpCodes.Ldarg_1);
                    setterCode.Emit(OpCodes.Stfld, fld);
                }
                else
                {
                    // use basetype to define the property
                    setterCode.Emit(OpCodes.Ldarg_0);
                    setterCode.Emit(OpCodes.Ldarg_1);
                    setterCode.Emit(OpCodes.Call, baseTypeProp.SetMethod);
                }

                
                // raise the property changed event
                if (isNotifyPropertyChanged)
                {
                    MethodInfo onPropertyChangedMI = null;
                    if (baseType != null && baseType.FindInterfaces(
                        (type, filter) => type.FullName.Contains(filter.ToString()),
                        typeof(INotifyPropertyChanged).FullName).Any())
                    {
                        onPropertyChangedMI = baseType.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                            .SingleOrDefault(mi => 
                                   mi.Name.Equals("OnPropertyChanged") 
                                && mi.GetParameters().Length == 1 
                                && mi.GetParameters()[0].ParameterType.Equals(typeof(string)) );
                    }

                    if (onPropertyChangedMI == null)
                    {
                        // implement the ProperyChanged event raise
                        setterCode.Emit(OpCodes.Ldarg_0);
                        setterCode.Emit(OpCodes.Ldfld, propertyChanged);
                        setterCode.Emit(OpCodes.Ldnull);
                        setterCode.Emit(OpCodes.Cgt_Un);
                        setterCode.Emit(OpCodes.Brfalse_S, returnLabel);
                        setterCode.Emit(OpCodes.Ldarg_0);
                        setterCode.Emit(OpCodes.Ldfld, propertyChanged);
                        setterCode.Emit(OpCodes.Ldarg_0);
                        setterCode.Emit(OpCodes.Ldstr, member.Name);
                        setterCode.Emit(OpCodes.Newobj, typeof(PropertyChangedEventArgs).GetConstructor(new[] { typeof(string) }));
                        setterCode.Emit(OpCodes.Callvirt, typeof(PropertyChangedEventHandler).GetMethod("Invoke", BindingFlags.Public | BindingFlags.Instance));
                    }
                    else
                    {
                        setterCode.Emit(OpCodes.Ldarg_0);
                        setterCode.Emit(OpCodes.Ldstr, member.Name);
                        setterCode.Emit(OpCodes.Callvirt, onPropertyChangedMI);
                    }
                }

                // wire up the new changed event handler
                if (isNotifyPropertyChanged && modelPropertyChanged != null)
                {

                    setterCode.Emit(OpCodes.Ldarg_0);
                    if (baseTypeProp == null)
                    {
                        setterCode.Emit(OpCodes.Ldfld, fld);
                    }
                    else
                    {
                        setterCode.Emit(OpCodes.Callvirt, baseTypeProp.GetMethod);
                    }
                    setterCode.Emit(OpCodes.Ldnull);
                    setterCode.Emit(OpCodes.Cgt_Un);
                    var subModelIsNull2 = setterCode.DefineLabel();
                    setterCode.Emit(OpCodes.Brfalse_S, subModelIsNull2);
                    setterCode.Emit(OpCodes.Ldarg_0);
                    if (baseTypeProp == null)
                    {
                        setterCode.Emit(OpCodes.Ldfld, fld);
                    }
                    else
                    {
                        setterCode.Emit(OpCodes.Callvirt, baseTypeProp.GetMethod);
                    }
                    setterCode.Emit(OpCodes.Ldarg_0);
                    setterCode.Emit(OpCodes.Ldftn, modelPropertyChanged);
                    setterCode.Emit(OpCodes.Newobj, typeof(PropertyChangedEventHandler).GetConstructor(new Type[] { typeof(object), typeof(IntPtr) }));
                    setterCode.Emit(OpCodes.Callvirt, typeof(INotifyPropertyChanged).GetEvent("PropertyChanged").AddMethod);
                    setterCode.MarkLabel(subModelIsNull2);
                }

                // wire up the new changing event handler
                if (isNotifyPropertyChanging && modelPropertyChanging != null)
                {

                    setterCode.Emit(OpCodes.Ldarg_0);
                    if (baseTypeProp == null)
                    {
                        setterCode.Emit(OpCodes.Ldfld, fld);
                    }
                    else
                    {
                        setterCode.Emit(OpCodes.Callvirt, baseTypeProp.GetMethod);
                    }
                    setterCode.Emit(OpCodes.Ldnull);
                    setterCode.Emit(OpCodes.Cgt_Un);
                    var subModelIsNull = setterCode.DefineLabel();
                    setterCode.Emit(OpCodes.Brfalse_S, subModelIsNull);
                    setterCode.Emit(OpCodes.Ldarg_0);
                    if (baseTypeProp == null)
                    {
                        setterCode.Emit(OpCodes.Ldfld, fld);
                    }
                    else
                    {
                        setterCode.Emit(OpCodes.Callvirt, baseTypeProp.GetMethod);
                    }
                    setterCode.Emit(OpCodes.Ldarg_0);
                    setterCode.Emit(OpCodes.Ldftn, modelPropertyChanging);
                    setterCode.Emit(OpCodes.Newobj, typeof(PropertyChangingEventHandler).GetConstructor(new Type[] { typeof(object), typeof(IntPtr) }));
                    setterCode.Emit(OpCodes.Callvirt, typeof(INotifyPropertyChanging).GetEvent("PropertyChanging").AddMethod);
                    setterCode.MarkLabel(subModelIsNull);
                }
                

                setterCode.MarkLabel(returnLabel);
                setterCode.Emit(OpCodes.Ret);
                property.SetSetMethod(setter);
            }

            return property;
        }

        private static MethodInfo BuildForwardedPropertyChangingHandler(Type interfaceType, TypeBuilder typeBuilder, Type baseType, PropertyInfo member, FieldInfo propertyChanging)
        {
            var method = typeBuilder.DefineMethod(member.Name + "_PropertyChanging",
                    MethodAttributes.Family
                    | MethodAttributes.HideBySig
                    | MethodAttributes.NewSlot
                    | MethodAttributes.Virtual
                    | MethodAttributes.Final,
                    CallingConventions.Standard,
                    typeof(void),
                    new Type[] { typeof(object), typeof(PropertyChangingEventArgs) });

            var code = method.GetILGenerator();
            var returnLabel = code.DefineLabel();

            MethodInfo onPropertyChangingMI = null;
            if (baseType != null && baseType.FindInterfaces(
                (type, filter) => type.FullName.Contains(filter.ToString()),
                typeof(INotifyPropertyChanged).FullName).Any())
            {
                onPropertyChangingMI = baseType.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                    .SingleOrDefault(mi =>
                           mi.Name.Equals("OnPropertyChanging")
                        && mi.GetParameters().Length == 1
                        && mi.GetParameters()[0].ParameterType.Equals(typeof(string)));
            }

            if (onPropertyChangingMI == null)
            {
                // implement the ProperyChanged event raise
                code.Emit(OpCodes.Ldarg_0);
                code.Emit(OpCodes.Ldfld, propertyChanging);
                code.Emit(OpCodes.Ldnull);
                code.Emit(OpCodes.Cgt_Un);
                code.Emit(OpCodes.Brfalse_S, returnLabel);
                code.Emit(OpCodes.Ldarg_0);
                code.Emit(OpCodes.Ldfld, propertyChanging);
                code.Emit(OpCodes.Ldarg_0);
                code.Emit(OpCodes.Ldstr, member.Name + ".");
                code.Emit(OpCodes.Ldarg_2);
                code.Emit(OpCodes.Callvirt, typeof(PropertyChangingEventArgs).GetProperty("PropertyName").GetMethod);
                code.Emit(OpCodes.Call, typeof(string).GetMethod("Concat", new Type[] { typeof(string), typeof(string) }));
                code.Emit(OpCodes.Newobj, typeof(PropertyChangingEventArgs).GetConstructor(new[] { typeof(string) }));
                code.Emit(OpCodes.Callvirt, typeof(PropertyChangingEventHandler).GetMethod("Invoke", BindingFlags.Public | BindingFlags.Instance));
            }
            else
            {
                code.Emit(OpCodes.Ldarg_0);
                code.Emit(OpCodes.Ldstr, member.Name + ".");
                code.Emit(OpCodes.Ldarg_2);
                code.Emit(OpCodes.Callvirt, typeof(PropertyChangingEventArgs).GetProperty("PropertyName").GetMethod);
                code.Emit(OpCodes.Call, typeof(string).GetMethod("Concat", new Type[] { typeof(string), typeof(string) }));
                code.Emit(OpCodes.Callvirt, onPropertyChangingMI);
            }
            code.MarkLabel(returnLabel);
            code.Emit(OpCodes.Ret);



            return method;
        }

        private static MethodInfo BuildForwardedPropertyChangedHandler(Type interfaceType, TypeBuilder typeBuilder, Type baseType, PropertyInfo member, FieldInfo propertyChangedEvent)
        {
            var method = typeBuilder.DefineMethod(member.Name + "_PropertyChanged",
                MethodAttributes.Family
                | MethodAttributes.HideBySig
                | MethodAttributes.NewSlot
                | MethodAttributes.Virtual
                | MethodAttributes.Final,
                CallingConventions.Standard,
                typeof(void),
                new Type[] { typeof(object), typeof(PropertyChangedEventArgs) });

            var code = method.GetILGenerator();
            var returnLabel = code.DefineLabel();

            MethodInfo onPropertyChangedMI = null;
            if (baseType != null && baseType.FindInterfaces(
                (type, filter) => type.FullName.Contains(filter.ToString()),
                typeof(INotifyPropertyChanged).FullName).Any())
            {
                onPropertyChangedMI = baseType.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                    .SingleOrDefault(mi =>
                           mi.Name.Equals("OnPropertyChanged")
                        && mi.GetParameters().Length == 1
                        && mi.GetParameters()[0].ParameterType.Equals(typeof(string)));
            }

            if (onPropertyChangedMI == null)
            {
                // implement the ProperyChanged event raise
                code.Emit(OpCodes.Ldarg_0);
                code.Emit(OpCodes.Ldfld, propertyChangedEvent);
                code.Emit(OpCodes.Ldnull);
                code.Emit(OpCodes.Cgt_Un);
                code.Emit(OpCodes.Brfalse_S, returnLabel);
                code.Emit(OpCodes.Ldarg_0);
                code.Emit(OpCodes.Ldfld, propertyChangedEvent);
                code.Emit(OpCodes.Ldarg_0);
                code.Emit(OpCodes.Ldstr, member.Name + ".");
                code.Emit(OpCodes.Ldarg_2);
                code.Emit(OpCodes.Callvirt, typeof(PropertyChangedEventArgs).GetProperty("PropertyName").GetMethod);
                code.Emit(OpCodes.Call, typeof(string).GetMethod("Concat", new Type[] { typeof(string), typeof(string) }));
                code.Emit(OpCodes.Newobj, typeof(PropertyChangedEventArgs).GetConstructor(new[] { typeof(string) }));
                code.Emit(OpCodes.Callvirt, typeof(PropertyChangedEventHandler).GetMethod("Invoke", BindingFlags.Public | BindingFlags.Instance));
            }
            else
            {
                code.Emit(OpCodes.Ldarg_0);
                code.Emit(OpCodes.Ldstr, member.Name + ".");
                code.Emit(OpCodes.Ldarg_2);
                code.Emit(OpCodes.Callvirt, typeof(PropertyChangedEventArgs).GetProperty("PropertyName").GetMethod);
                code.Emit(OpCodes.Call, typeof(string).GetMethod("Concat", new Type[] { typeof(string), typeof(string) }));
                code.Emit(OpCodes.Callvirt, onPropertyChangedMI);
            }
            code.MarkLabel(returnLabel);
            code.Emit(OpCodes.Ret);



            return method;
        }

        /// <summary>
        /// Specifically built to provide a complete list of declared and inherited members for interface type declarations
        /// </summary>
        /// <param name="interfaceType"></param>
        /// <returns></returns>
        public static IEnumerable<MemberInfo> GetMembers(Type interfaceType)
        {
            if (!interfaceType.IsInterface) throw new InvalidOperationException("This method is designed to work for interfaces only.");

            List<MemberInfo> members = new List<MemberInfo>();
            GetMembersRecurse(interfaceType, ref members);
            return members;
        }

        protected static void GetMembersRecurse(Type interfaceType, ref List<MemberInfo> members)
        {
            foreach (var t in interfaceType.FindInterfaces((a, b) => true, true))
            {
                //if ((interfaceType.BaseType != null && !interfaceType.BaseType.Equals(typeof(object))))
                GetMembersRecurse(t, ref members);
            }

            foreach (var member in interfaceType.GetMembers(BindingFlags.Public | BindingFlags.Instance))
            {
                if (!members.Contains(member, new MemberInfoComparer()))
                    members.Add(member);
            }
        }

        protected static MethodInfo GetSetter(PropertyInfo propertyInfo, IEnumerable<MemberInfo> members)
        {
            return members.OfType<MethodInfo>()
                .SingleOrDefault(mi =>
                    mi.IsSpecialName
                && mi.Name.Equals("set_" + propertyInfo.Name)
                && mi.DeclaringType.Equals(propertyInfo.DeclaringType));
        }

        protected static MethodInfo GetGetter(PropertyInfo propertyInfo, IEnumerable<MemberInfo> members)
        {
            return members.OfType<MethodInfo>()
                .SingleOrDefault(mi =>
                    mi.IsSpecialName
                && mi.Name.Equals("get_" + propertyInfo.Name)
                && mi.DeclaringType.Equals(propertyInfo.DeclaringType));
        }

        public bool Equals(PropertyInfo x, PropertyInfo y)
        {
            return x.Name.Equals(y.Name) && x.PropertyType.Equals(y.PropertyType);
        }

        public int GetHashCode(PropertyInfo obj)
        {
            return obj.GetHashCode();
        }

        public class PropertyInfoComparer : IEqualityComparer<PropertyInfo>
        {
            public bool Equals(PropertyInfo x, PropertyInfo y)
            {
                return x.Name.Equals(y.Name) && x.PropertyType.Equals(y.PropertyType);
            }

            public int GetHashCode(PropertyInfo obj)
            {
                return obj.GetHashCode();
            }
        }

        public class MemberInfoComparer : IEqualityComparer<MemberInfo>
        {
            public bool Equals(MemberInfo x, MemberInfo y)
            {
                var equals = x != null
                    && y != null
                    && x.Name.Equals(y.Name)
                    && x.MemberType.Equals(y.MemberType)
                    && (x is MethodInfo && y is MethodInfo ? ParametersEqual((MethodInfo)x, (MethodInfo)y) : false);
                return equals;
            }

            private bool ParametersEqual(MethodInfo methodInfo1, MethodInfo methodInfo2)
            {
                ParameterInfo[] p1 = methodInfo1.GetParameters();
                Type pRet1 = methodInfo1.ReturnType;
                ParameterInfo[] p2 = methodInfo2.GetParameters();
                Type pRet2 = methodInfo1.ReturnType;
                return pRet1.Equals(pRet2)
                    && ParametersEqual(p1, p2)
                    && GenericArgsEqual(methodInfo1, methodInfo2);

            }

            private bool GenericArgsEqual(MethodInfo methodInfo1, MethodInfo methodInfo2)
            {
                return methodInfo1.IsGenericMethod == methodInfo2.IsGenericMethod
                    && methodInfo1.IsGenericMethod ? GenericArgsEqual(methodInfo1.GetGenericArguments(), methodInfo2.GetGenericArguments()) : true;
            }

            private bool GenericArgsEqual(Type[] type1, Type[] type2)
            {
                var areEqual = type1.Length == type2.Length;
                if (areEqual)
                {
                    for (int i = 0; i < type1.Length; i++)
                    {
                        if (!type1[i].Equals(type2[i]))
                        {
                            areEqual = false;
                            break;
                        }
                    }
                }
                return areEqual;
            }

            private bool ParametersEqual(ParameterInfo[] p1, ParameterInfo[] p2)
            {
                if (p1.Length.Equals(p2.Length))
                {
                    for (int p = 0; p < p1.Length; p++)
                    {
                        if (!p1[p].ParameterType.Equals(p2[p].ParameterType)) return false;
                    }
                    return true;
                }
                else return false;
            }

            public int GetHashCode(MemberInfo obj)
            {
                return obj.GetHashCode();
            }
        }

    }

    public class Test : IModel<long>, _IOwnedModel
    {
        public DateTime Created
        {
            get;

            set;
        }

        public bool IsDeleted
        {
            get;

            set;
        }

        public bool IsNew
        {
            get
            {
                throw new NotImplementedException();
            }
        }

        public long Key
        {
            get;

            set;
        }

        public Type ModelType
        {
            get;
        }

        public DateTime Modified
        {
            get;

            set;
        }

        [BinarySerializable(100000)]
        public IOrgUnit Owner
        {
            get;
            set;
        }

        IOrgUnit IOwnedModel.Owner
        {
            get
            {
                return this.Owner;
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        public event PropertyChangingEventHandler PropertyChanging;

        public IEnumerable<AuditedChange> Compare(IModel model, string prefix)
        {
            throw new NotImplementedException();
        }

        public string GetKey()
        {
            throw new NotImplementedException();
        }

        public void SetKey(string value)
        {
            throw new NotImplementedException();
        }
    }

}

using Common.Extensions;
using Data.Core.ComponentModel;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Data.Core.Compilation
{
    public static class AnonymousTypeBuilder
    {
        static Dictionary<string, Type> _buildCache = new Dictionary<string, Type>();
        static AssemblyName _asmName = new AssemblyName() { Name = "Anonymous.Types" };
        static ModuleBuilder _modBuilder;
        static AssemblyBuilder _asmBuilder;
        static ulong _typeNumber = 0;

        static AnonymousTypeBuilder()
        {
            _asmBuilder = Thread.GetDomain().DefineDynamicAssembly(_asmName, AssemblyBuilderAccess.RunAndSave);
            _modBuilder = _asmBuilder.DefineDynamicModule(_asmName.Name, _asmName.Name + ".dll", true);
        }

        public static Type CreateType(Dictionary<string, Expression> members, string typeName = null)
        {
            if (null == members)
                throw new ArgumentNullException("fields");
            if (0 == members.Count)
                throw new ArgumentOutOfRangeException("members", "members must have at least 1 definition");

            lock (_buildCache)
            {
                var key = string.IsNullOrEmpty(typeName) ? GetTypeKey(members) : typeName;
                Type type;
                if (!TryGetType(typeName, out type) && !TryGetModelType(members, out type))
                {
                    if (!_buildCache.TryGetValue(key, out type))
                    {
                        _typeNumber++;
                        var className = string.IsNullOrEmpty(typeName) ? "Anon_" + _typeNumber : typeName;

                        var typeBuilder = _modBuilder.DefineType(className, TypeAttributes.Public | TypeAttributes.Class | TypeAttributes.Serializable);
                        foreach (var member in members)
                        {
                            CreateProperty(typeBuilder, member.Key, member.Value.Type);
                        }
                        type = typeBuilder.CreateType();
                        _buildCache.Add(key, type);
                    }
                }
                return type;
            }
        }

        private static bool TryGetModelType(Dictionary<string, Expression> members, out Type type)
        {
            Expression modelType;
            type = null;
            if (members.TryGetValue("@@type", out modelType) 
                && modelType is ConstantExpression
                && ModelTypeManager.TryGetModelType(((ConstantExpression)modelType).Value.ToString(), out type))
            {
                type = RuntimeModelBuilder.CreateModelType(type, ModelTypeConverter.ModelBaseType);
                return true;
            }
            return false;
        }

        private static void CreateProperty(TypeBuilder typeBuilder, string key, Type value)
        {
            var fld = typeBuilder.DefineField("_" + key, value, FieldAttributes.Public);
            fld.SetCustomAttribute(new CustomAttributeBuilder(typeof(Newtonsoft.Json.JsonIgnoreAttribute).GetConstructor(Type.EmptyTypes), new object[0]));

            var prop = typeBuilder.DefineProperty(key,
                PropertyAttributes.HasDefault,
                value,
                null);

            var getter = typeBuilder.DefineMethod("get_" + key,
                MethodAttributes.Public | MethodAttributes.SpecialName | MethodAttributes.HideBySig,
                value,
                Type.EmptyTypes);
            var gCode = getter.GetILGenerator();
            gCode.Emit(OpCodes.Ldarg_0);
            gCode.Emit(OpCodes.Ldfld, fld);
            gCode.Emit(OpCodes.Ret);

            var setter = typeBuilder.DefineMethod("set_" + key,
                MethodAttributes.Public | MethodAttributes.SpecialName | MethodAttributes.HideBySig,
                typeof(void),
                new Type[] { value });
            var sCode = setter.GetILGenerator();
            sCode.Emit(OpCodes.Ldarg_0);
            sCode.Emit(OpCodes.Ldarg_1);
            sCode.Emit(OpCodes.Stfld, fld);
            sCode.Emit(OpCodes.Ret);

            prop.SetGetMethod(getter);
            prop.SetSetMethod(setter);
        }

        //private static void CreateMethod(TypeBuilder typeBuilder, string key, Expression value)
        //{
        //    MethodBuilder method;
        //    if (value.Type.Name.StartsWith("Func"))
        //    {
        //        var args = value.Type.GetGenericArguments();
        //        method = typeBuilder.DefineMethod(
        //            key,
        //            MethodAttributes.Public | MethodAttributes.Static,
        //            args.Last(),
        //            args.Take(args.Length - 1).ToArray());
        //    }
        //    else
        //    {
        //        var args = value.Type.GetGenericArguments();
        //        method = typeBuilder.DefineMethod(
        //            key,
        //            MethodAttributes.Public | MethodAttributes.Static,
        //            typeof(void),
        //            args);
        //    }

        //    ((LambdaExpression)value).CompileToMethod(method);
        //}

        private static string GetTypeKey(Dictionary<string, Expression> members)
        {
            var sb = new StringBuilder();
            foreach (var member in members)
            {
                sb.Append(member.Key);
                sb.Append(member.Value.Type.FullName);
            }
            return sb.ToString().ToBase64MD5();
        }

        public static bool TryGetType(string typeName, out Type type)
        {
            if (string.IsNullOrEmpty(typeName))
            {
                type = null;
                return false;
            }
            lock (_buildCache)
                return _buildCache.TryGetValue(typeName, out type);
        }
    }

    public class AnonType
    {
        long _age;
        public long Age { get { return _age; } set { _age = value; } }
    }
}

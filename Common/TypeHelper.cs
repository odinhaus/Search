using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Common
{
    public class ResolveTypeEventArgs
    {
        public ResolveTypeEventArgs(string typeName)
        {
            TypeName = typeName;
        }

        public string TypeName { get; private set; }
        public Type ResolvedType { get; set; }
        public bool IsResolved
        {
            get
            {
                return ResolvedType != null;
            }
        }
    }

    public static class TypeHelper
    {

        #region Fields
        #region Static Fields
        static List<Action<ResolveTypeEventArgs>> _resolve = new List<Action<ResolveTypeEventArgs>>();
        static Dictionary<string, Type> _resolvedTypes = new Dictionary<string, Type>();
        #endregion Static Fields

        #region Instance Fields
        #endregion Instance Fields
        #endregion Fields

        #region Event Declarations
        #endregion Event Declarations

        #region Constructors
        #region Public
        #endregion Public

        #region Private
        #endregion Private

        #region Protected
        #endregion Protected
        #endregion  Constructors

        #region Properties
        #region Public
        #endregion Public

        #region Private
        #endregion Private

        #region Protected
        #endregion Protected
        #endregion Properties

        #region Methods
        #region Public
        public static void RegisterResolver(Action<ResolveTypeEventArgs> resolver)
        {
            _resolve.Add(resolver);
        }

        public static void UnregisterResolver(Action<ResolveTypeEventArgs> resolver)
        {
            _resolve.Remove(resolver);
        }

        public static Type GetType(string typeName, bool bThrowIfNotFound)
        {
            Type t = GetType(typeName);
            if (t == null)
                throw new ApplicationException(String.Format("Unable to resolve type '{0}'.", typeName));
            return t;
        }

        //========================================================================================================//
        /// <summary>
        /// Parses the provided string and attempts to resolve a Type for the string
        /// </summary>
        /// <param name="typeName"></param>
        /// <returns>the configured type if it can be found/created, otherwise returns null</returns>
        public static Type GetType(string typeName)
        {
            string[] parts = typeName.Split(',');
            Type retType = null;
            try
            {

                lock (_resolvedTypes)
                {
                    if (_resolvedTypes.ContainsKey(typeName))
                        return _resolvedTypes[typeName];

                    switch (parts.Length)
                    {
                        case 5:
                            {
                                Assembly assem = Assembly.Load(string.Format("{0}, {1}, {2}, {3}", parts[1].Trim(), parts[2].Trim(), parts[3].Trim(), parts[4].Trim()));
                                retType = assem.GetType(parts[0].Trim(), true, true);
                                break;
                            }
                        case 4:
                            {
                                Assembly assem = Assembly.Load(string.Format("{0}, {1}, {2}", parts[1].Trim(), parts[2].Trim(), parts[3].Trim()));
                                retType = assem.GetType(parts[0].Trim(), true, true);
                                break;
                            }
                        case 3:
                            {
                                Assembly assem = Assembly.Load(string.Format("{0}, {1}", parts[1].Trim(), parts[2].Trim()));
                                retType = assem.GetType(parts[0].Trim(), true, true);
                                break;
                            }
                        case 2: // the assemblyname, typename was supplied
                            {
                                Assembly assem = Assembly.Load(parts[1].Trim());
                                retType = assem.GetType(parts[0].Trim(), true, true);
                                break;
                            }
                        default:
                            {
                                if (!TryGetCommonType(typeName, out retType))
                                {
                                    retType = Type.GetType(typeName, true, true);
                                }
                                break;
                            }
                    }

                    _resolvedTypes.Add(typeName, retType);
                }
                return retType;
            }
            catch
            {
                if (parts.Length >= 1)
                {
                    foreach (var asm in AppDomain.CurrentDomain.GetAssemblies()
                        .Where(a => parts.Length == 1 || a.GetName().Name.Equals(parts[1].Trim(), StringComparison.CurrentCultureIgnoreCase)))
                    {
                        if (asm != null)
                        {
                            retType = asm.GetTypes().SingleOrDefault(t => t.FullName.Equals(parts[0].Trim()));
                            if (retType != null)
                            {
                                _resolvedTypes.Add(typeName, retType);
                                return retType;
                            }
                        }
                    }
                }

                var resolve = new ResolveTypeEventArgs(typeName);
                foreach (var mc in _resolve)
                {
                    mc(resolve);
                    if (resolve.IsResolved)
                    {
                        _resolvedTypes.Add(typeName, resolve.ResolvedType);
                        return resolve.ResolvedType;
                    }
                }

                return null;
            }
        }
        //========================================================================================================//


        private static bool TryGetCommonType(string typeName, out Type type)
        {
            switch (typeName.ToLower())
            {
                default:
                    {
                        type = null;
                        return false;
                    }
                case "object":
                    {
                        type = typeof(object);
                        return true;
                    }
                case "string":
                    {
                        type = typeof(string);
                        return true;
                    }
                case "byte":
                    {
                        type = typeof(byte);
                        return true;
                    }
                case "sbyte":
                    {
                        type = typeof(sbyte);
                        return true;
                    }
                case "char":
                    {
                        type = typeof(char);
                        return true;
                    }
                case "int16":
                case "short":
                    {
                        type = typeof(short);
                        return true;
                    }
                case "uint16":
                case "ushort":
                    {
                        type = typeof(ushort);
                        return true;
                    }
                case "int32":
                case "int":
                    {
                        type = typeof(int);
                        return true;
                    }
                case "uint32":
                case "unit":
                    {
                        type = typeof(uint);
                        return true;
                    }
                case "int64":
                case "long":
                    {
                        type = typeof(long);
                        return true;
                    }
                case "uint64":
                case "ulong":
                    {
                        type = typeof(ulong);
                        return true;
                    }
                case "float":
                    {
                        type = typeof(float);
                        return true;
                    }
                case "double":
                    {
                        type = typeof(double);
                        return true;
                    }
                case "decimal":
                    {
                        type = typeof(decimal);
                        return true;
                    }
                case "datetime":
                    {
                        type = typeof(DateTime);
                        return true;
                    }
            }
        }


        //========================================================================================================//
        /// <summary>
        /// Gets the type in the provided assembly by name
        /// </summary>
        /// <param name="typeName"></param>
        /// <param name="assemblyName"></param>
        /// <returns></returns>
        public static Type GetType(string typeName, string assemblyName)
        {
            try
            {
                Assembly assem;
                if (File.Exists(assemblyName))
                    assem = Assembly.LoadFrom(assemblyName);
                else
                    assem = Assembly.Load(assemblyName.Trim());
                return assem.GetType(typeName.Trim());
            }
            catch
            {
                return null;
            }
        }
        //========================================================================================================//


        //========================================================================================================//
        /// <summary>
        /// Creates an instance of the type specified by typeName
        /// </summary>
        /// <param name="typeName"></param>
        /// <param name="activationArgs"></param>
        /// <returns>new instance of type, otherwise null</returns>
        public static object CreateType(string typeName, object[] activationArgs)
        {
            Type type = GetType(typeName);
            if (type != null)
            {
                return Activator.CreateInstance(type, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance,
                    null,
                    activationArgs,
                    Thread.CurrentThread.CurrentCulture);
            }
            else
            {
                return null;
            }
        }
        //========================================================================================================//


        /// <summary>
        /// Returns true if the current type implements the provided concrete interface type, or generic interface type
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="type"></param>
        /// <returns></returns>
        public static bool Implements<T>(this Type type)
        {
            return Implements(type, typeof(T));
        }

        /// <summary>
        /// Returns true if the current type implements the provided concrete interface type, or generic interface type
        /// </summary>
        /// <param name="type"></param>
        /// <param name="interfaceType"></param>
        /// <returns></returns>
        public static bool Implements(this Type type, Type interfaceType)
        {
            return type == interfaceType
                || (type.IsConstructedGenericType && type.GetGenericTypeDefinition() == interfaceType)
                || type.GetTypeInfo().ImplementedInterfaces.Any(i =>
                    i.Equals(interfaceType) || (i.IsConstructedGenericType && i.GetGenericTypeDefinition().Equals(interfaceType)));
        }

        public static bool IsTypeOrSubtypeOf<T>(this Type type)
        {
            return type == typeof(T) || type.IsSubclassOf(typeof(T));
        }

        public static bool IsConvertibleTo(this Type sourceType, Type targetType)
        {
            if (sourceType.Equals(targetType)) return true;
            if (sourceType.IsSubclassOf(targetType)) return true;
            if (targetType.IsInterface && sourceType.Implements(targetType)) return true;
            return false;
        }

        public static Type ResolveElementType(this Type seqType)
        {
            if (seqType.IsArray)
            {
                return seqType.GetElementType();
            }

            Type ienum = FindIEnumerable(seqType);
            if (ienum == null)
            {
                return seqType;
            }
            return ienum.GetTypeInfo().GenericTypeArguments[0];
        }
        private static Type FindIEnumerable(Type seqType)
        {
            if (seqType == null || seqType == typeof(string))
                return null;
            if (seqType.IsArray)
                return typeof(IEnumerable<>).MakeGenericType(seqType.GetElementType());
            if (seqType.GetTypeInfo().IsGenericType)
            {
                foreach (Type arg in seqType.GetTypeInfo().GenericTypeArguments)
                {
                    Type ienum = typeof(IEnumerable<>).MakeGenericType(arg);
                    if (ienum.GetTypeInfo().IsAssignableFrom(seqType.GetTypeInfo()))
                    {
                        return ienum;
                    }
                }
            }
            Type[] ifaces = seqType.GetTypeInfo().ImplementedInterfaces.ToArray();
            if (ifaces != null && ifaces.Length > 0)
            {
                foreach (Type iface in ifaces)
                {
                    Type ienum = FindIEnumerable(iface);
                    if (ienum != null) return ienum;
                }
            }
            if (seqType.GetTypeInfo().BaseType != null && seqType.GetTypeInfo().BaseType != typeof(object))
            {
                return FindIEnumerable(seqType.GetTypeInfo().BaseType);
            }
            return null;
        }

        public static bool IsVirtual(this MemberInfo mi)
        {
            return mi.MemberType == MemberTypes.Property && ((PropertyInfo)mi).GetGetMethod().IsVirtual;
        }


        public static Type GetSequenceType(Type elementType)
        {
            return typeof(IEnumerable<>).MakeGenericType(elementType);
        }

        public static Type GetElementType(Type seqType)
        {
            Type ienum = FindIEnumerable(seqType);
            if (ienum == null) return seqType;
            return ienum.GetGenericArguments()[0];
        }

        public static bool IsNullableType(Type type)
        {
            return type != null && type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Nullable<>);
        }

        public static bool IsNullAssignable(Type type)
        {
            return !type.IsValueType || IsNullableType(type);
        }

        public static Type GetNonNullableType(Type type)
        {
            if (IsNullableType(type))
            {
                return type.GetGenericArguments()[0];
            }
            return type;
        }

        public static Type GetNullAssignableType(Type type)
        {
            if (!IsNullAssignable(type))
            {
                return typeof(Nullable<>).MakeGenericType(type);
            }
            return type;
        }

        public static ConstantExpression GetNullConstant(Type type)
        {
            return Expression.Constant(null, GetNullAssignableType(type));
        }

        public static Type GetMemberType(MemberInfo mi)
        {
            FieldInfo fi = mi as FieldInfo;
            if (fi != null) return fi.FieldType;
            PropertyInfo pi = mi as PropertyInfo;
            if (pi != null) return pi.PropertyType;
            EventInfo ei = mi as EventInfo;
            if (ei != null) return ei.EventHandlerType;
            MethodInfo meth = mi as MethodInfo;  // property getters really
            if (meth != null) return meth.ReturnType;
            TypeInfo ti = mi as TypeInfo;
            if (ti != null) return ti;
            return null;
        }

        public static object GetDefault(Type type)
        {
            bool isNullable = !type.IsValueType || TypeHelper.IsNullableType(type);
            if (!isNullable)
                return Activator.CreateInstance(type);
            return null;
        }

        public static bool IsReadOnly(MemberInfo member)
        {
            switch (member.MemberType)
            {
                case MemberTypes.Field:
                    return (((FieldInfo)member).Attributes & FieldAttributes.InitOnly) != 0;
                case MemberTypes.Property:
                    PropertyInfo pi = (PropertyInfo)member;
                    return !pi.CanWrite || pi.GetSetMethod() == null;
                default:
                    return true;
            }
        }

        public static bool IsInteger(Type type)
        {
            Type nnType = GetNonNullableType(type);
            switch (Type.GetTypeCode(type))
            {
                case TypeCode.SByte:
                case TypeCode.Int16:
                case TypeCode.Int32:
                case TypeCode.Int64:
                case TypeCode.Byte:
                case TypeCode.UInt16:
                case TypeCode.UInt32:
                case TypeCode.UInt64:
                    return true;
                default:
                    return false;
            }
        }

        public static bool IsEnumerableOrArray(MemberInfo member)
        {
            var type = GetMemberType(member);
            return type.IsArray || type.Implements(typeof(IEnumerable<>));
        }

        public static PropertyInfo[] GetPublicProperties(this Type type)
        {
            if (type.IsInterface)
            {
                var propertyInfos = new List<PropertyInfo>();

                var considered = new List<Type>();
                var queue = new Queue<Type>();
                considered.Add(type);
                queue.Enqueue(type);
                while (queue.Count > 0)
                {
                    var subType = queue.Dequeue();
                    foreach (var subInterface in subType.GetInterfaces())
                    {
                        if (considered.Contains(subInterface)) continue;

                        considered.Add(subInterface);
                        queue.Enqueue(subInterface);
                    }

                    var typeProperties = subType.GetProperties(
                        BindingFlags.FlattenHierarchy
                        | BindingFlags.Public
                        | BindingFlags.Instance);

                    var newPropertyInfos = typeProperties
                        .Where(x => !propertyInfos.Contains(x));

                    propertyInfos.InsertRange(0, newPropertyInfos);
                }

                return propertyInfos.ToArray();
            }

            return type.GetProperties(BindingFlags.FlattenHierarchy
                | BindingFlags.Public | BindingFlags.Instance);
        }

        public static PropertyInfo GetPublicProperty(this Type type, string name)
        {
            return GetPublicProperties(type).SingleOrDefault(p => p.Name.Equals(name));
        }

        #endregion Public

        #region Private
        #endregion Private

        #region Protected
        #endregion Protected
        #endregion Methods

        #region Event Handlers and Callbacks
        #region Public
        #endregion Public

        #region Private
        #endregion Private

        #region Protected
        #endregion Protected
        #endregion Event Handlers and Callbacks
    }
}

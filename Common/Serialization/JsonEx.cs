using Newtonsoft.Json.Serialization;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Common.Serialization
{
    public static class JsonEx
    {
        static IContractResolver _resolver;
        static JsonEx()
        {
            _resolver = new DefaultContractResolver();
            try
            {
                _resolver = AppContext.Current.Container.GetInstance<IContractResolver>();
            }
            catch { }
        }
        public static string ToJson<T>(this T value)
        {
            if (typeof(T).Implements(typeof(IEnumerable<>))
                && typeof(T).GetGenericArguments().Length == 1
                && typeof(T).GetGenericArguments()[0].Implements(typeof(KeyValuePair<,>)))
            {
                var dict = new Dictionary<string, object>();
                var enumerator = ((IEnumerable)value).GetEnumerator();
                while (enumerator.MoveNext())
                {
                    var kvp = enumerator.Current;
                    var kvpType = kvp.GetType();
                    var key = kvpType.GetProperty("Key").GetValue(kvp).ToString();
                    var val = kvpType.GetProperty("Value").GetValue(kvp);
                    dict[key] = val;
                }
                return Newtonsoft.Json.JsonConvert.SerializeObject(dict, new Newtonsoft.Json.JsonSerializerSettings() { ContractResolver = _resolver, DateFormatHandling = Newtonsoft.Json.DateFormatHandling.IsoDateFormat });
            }
            else
                return Newtonsoft.Json.JsonConvert.SerializeObject(value, new Newtonsoft.Json.JsonSerializerSettings() { ContractResolver = _resolver, DateFormatHandling = Newtonsoft.Json.DateFormatHandling.IsoDateFormat });
        }

        public static T FromJson<T>(this string json)
        {
            return (T)FromJson(json, typeof(T));
        }


        public static object FromJson(this string json, Type objectType)
        {
            return Newtonsoft.Json.JsonConvert.DeserializeObject(
                json,
                objectType,
                new Newtonsoft.Json.JsonSerializerSettings() { ContractResolver = _resolver, DateFormatHandling = Newtonsoft.Json.DateFormatHandling.IsoDateFormat });
        }

        /// <summary>
        /// Returns true if the current type implements the provided concrete interface type, or generic interface type
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="type"></param>
        /// <returns></returns>
        //public static bool Implements<T>(this Type type)
        //{
        //    return Implements(type, typeof(T));
        //}

        /// <summary>
        /// Returns true if the current type implements the provided concrete interface type, or generic interface type
        /// </summary>
        /// <param name="type"></param>
        /// <param name="interfaceType"></param>
        /// <returns></returns>
        //public static bool Implements(this Type type, Type interfaceType)
        //{
        //    return type == interfaceType
        //        || (type.IsConstructedGenericType && type.GetGenericTypeDefinition() == interfaceType)
        //        || type.GetTypeInfo().ImplementedInterfaces.Any(i =>
        //            i.Equals(interfaceType) || (i.IsConstructedGenericType && i.GetGenericTypeDefinition().Equals(interfaceType)));
        //}
    }
}

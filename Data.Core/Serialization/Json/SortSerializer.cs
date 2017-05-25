using Common.Serialization;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Common.DI;
using Altus.Suffūz.Serialization;
using System.Linq.Expressions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Data.Core.Linq;
using Common;
using System.Reflection;

namespace Data.Core.Serialization.Json
{
    public class SortSerializer : SerializerBase<SortExpression>
    {
        public override void Register(IContainerMappings mappings)
        {
            IsRegistered = true;
        }

        protected override object OnDeserialize(byte[] source, Type targetType)
        {
            var jobj = JObject.Parse(SerializationContext.Instance.TextEncoding.GetString(source));

            return Deserialize(jobj);
        }

        public object Deserialize(JObject jobj)
        {
            var expression = (SortExpression)VisitJObject(jobj);

            return expression;
        }

        protected virtual Expression VisitJObject(JObject jobj)
        {
            var nodeType = (QueryExpressionType)Enum.Parse(typeof(QueryExpressionType), jobj.Property("NodeType").Value.Value<string>());
            switch(nodeType)
            {
                case QueryExpressionType.Sort:
                    {
                        return VisitSort(jobj);
                    }
                default:
                    {
                        return null;
                    }
            }
        }

        private Expression VisitSort(JObject jobj)
        {
            var exp = new SortExpression(jobj.Property("MemberName").Value.Value<string>(), (SortDirection)(long)((JValue)jobj.Property("SortDirection").Value).Value);
            return exp;
            //var typeName = jobj.Property("$type").Value.Value<string>();
            //return (Expression)Activator.CreateInstance(Type.GetType(typeName), 
            //    new object[] { jobj.Property("MemberName").Value.Value<string>(), (SortDirection)(long)((JValue)jobj.Property("SortDirection").Value).Value });
        }

        protected override IEnumerable<Type> OnGetSupportedTypes()
        {
            return new Type[] { typeof(SortExpression) };
        }

        protected override byte[] OnSerialize(object source)
        {
            return SerializationContext.Instance.TextEncoding.GetBytes(JsonConvert.SerializeObject(source, new JsonSerializerSettings() { TypeNameHandling = TypeNameHandling.None }));
        }

        protected override bool OnSupportsFormats(string format)
        {
            return StandardFormats.JSON.Equals(format);
        }
    }
}

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
    public class ExpressionSerializer : SerializerBase<PredicateExpression>
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
            var expression = (PredicateExpression)VisitJObject(jobj);

            return expression;
        }

        protected virtual Expression VisitJObject(JObject jobj)
        {
            var nodeType = (QueryExpressionType)Enum.Parse(typeof(QueryExpressionType), jobj.Property("NodeType").Value.Value<string>());
            switch(nodeType)
            {
                case QueryExpressionType.Predicate:
                    {
                        return VisitPredicate(jobj);
                    }
                case QueryExpressionType.And:
                    {
                        return VisitAnd(jobj);
                    }
                case QueryExpressionType.Contains:
                    {
                        return VisitContains(jobj);
                    }
                case QueryExpressionType.EQ:
                    {
                        return VisitEQ(jobj);
                    }
                case QueryExpressionType.GTE:
                    {
                        return VisitGTE(jobj);
                    }
                case QueryExpressionType.LTE:
                    {
                        return VisitLTE(jobj);
                    }
                case QueryExpressionType.GT:
                    {
                        return VisitGT(jobj);
                    }
                case QueryExpressionType.LT:
                    {
                        return VisitLT(jobj);
                    }
                case QueryExpressionType.NE:
                    {
                        return VisitNEQ(jobj);
                    }
                case QueryExpressionType.Or:
                    {
                        return VisitOr(jobj);
                    }
                case QueryExpressionType.StartsWith:
                    {
                        return VisitStartsWith(jobj);
                    }
                case QueryExpressionType.Traverse:
                    {
                        return VisitTraverse(jobj);
                    }
                default:
                    {
                        return null;
                    }
            }
        }


        private Expression VisitTraverse(JObject jobj)
        {
            var exp = new StartsWithExpression(jobj.Property("MemberName").Value.Value<string>(), new ScalarExpression(((JValue)jobj.Property("Value").Value).Value?.ToString() ?? "", typeof(string)));
            return exp;
        }

        private Expression VisitStartsWith(JObject jobj)
        {
            var exp = new StartsWithExpression(jobj.Property("MemberName").Value.Value<string>(), new ScalarExpression(((JValue)jobj.Property("Value").Value).Value?.ToString() ?? "", typeof(string)));
            return exp;
        }

        private Expression VisitOr(JObject jobj)
        {
            return new OrExpression(VisitJObject(jobj.Property("Left").Value as JObject), new ScalarExpression(VisitJObject(jobj.Property("Right").Value as JObject), typeof(object)));
        }

        private Expression VisitNEQ(JObject jobj)
        {

            var exp = new NEQExpression(jobj.Property("MemberName").Value.Value<string>(), new ScalarExpression(((JValue)jobj.Property("Value").Value).Value, typeof(object)));
            return exp;
        }

        private Expression VisitLT(JObject jobj)
        {
            var exp = new LTExpression(jobj.Property("MemberName").Value.Value<string>(), new ScalarExpression(((JValue)jobj.Property("Value").Value).Value, typeof(object)));
            return exp;
        }

        private Expression VisitAnd(JObject jobj)
        {
            return new AndExpression(VisitJObject(jobj.Property("Left").Value as JObject), VisitJObject(jobj.Property("Right").Value as JObject));
        }

        private Expression VisitLTE(JObject jobj)
        {
            var exp = new LTEExpression(jobj.Property("MemberName").Value.Value<string>(), new ScalarExpression(((JValue)jobj.Property("Value").Value).Value, typeof(object)));
            return exp;
        }

        private Expression VisitGTE(JObject jobj)
        {
            var exp = new GTEExpression(jobj.Property("MemberName").Value.Value<string>(), new ScalarExpression(((JValue)jobj.Property("Value").Value).Value, typeof(object)));
            return exp;
        }

        private Expression VisitGT(JObject jobj)
        {
            var exp = new GTExpression(jobj.Property("MemberName").Value.Value<string>(), new ScalarExpression(((JValue)jobj.Property("Value").Value).Value, typeof(object)));
            return exp;
        }

        private Expression VisitEQ(JObject jobj)
        {
            var exp = new EQExpression(jobj.Property("MemberName").Value.Value<string>(), new ScalarExpression(((JValue)jobj.Property("Value").Value).Value, typeof(object)));
            return exp;
        }

        private Expression VisitContains(JObject jobj)
        {
            var exp = new ContainsExpression(jobj.Property("MemberName").Value.Value<string>(), new ScalarExpression(((JValue)jobj.Property("Value").Value).Value?.ToString() ?? "", typeof(string)));
            return exp;
        }

        private Expression VisitPredicate(JObject jobj)
        {
            var body = jobj.Property("Body");
            return new PredicateExpression(VisitJObject(body.Value as JObject));
        }

        protected override IEnumerable<Type> OnGetSupportedTypes()
        {
            return new Type[] { typeof(PredicateExpression)};
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

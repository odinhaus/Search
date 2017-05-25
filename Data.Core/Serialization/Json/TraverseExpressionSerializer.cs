using Shs.Common.Serialization;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Shs.Common.DI;
using Altus.Suffūz.Serialization;
using System.Linq.Expressions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Shs.Data.Core.Linq;
using Shs.Common;
using System.Reflection;

namespace Shs.Data.Core.Serialization.Json
{
    public class TraverseExpressionSerializer : SerializerBase<TraverseExpression>
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
            var expression = (TraverseExpression)VisitJObject(jobj);

            return expression;
        }

        protected virtual Expression VisitJObject(JObject jobj)
        {
            if (jobj == null) return null;
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
                case QueryExpressionType.InEdgeNodeFilter:
                    {
                        return VisitInEdgeFilter(jobj);
                    }
                case QueryExpressionType.OutEdgeNodeFilter:
                    {
                        return VisitOutEdgeFilter(jobj);
                    }
                case QueryExpressionType.PathEdgeFilterMember:
                    {
                        return VisitPathEdgeFilterMember(jobj);
                    }
                case QueryExpressionType.PathNodeFilterMember:
                    {
                        return VisitPathNodeFilterMember(jobj);
                    }
                case QueryExpressionType.PathRootFilter:
                    {
                        return VisitPathRootFilter(jobj);
                    }
                case QueryExpressionType.TraverseOrigin:
                    {
                        return VisitTraverseOrigin(jobj);
                    }
                case QueryExpressionType.TraverseReturns:
                    {
                        return VisitTraverseReturns(jobj);
                    }
                default:
                    {
                        return null;
                    }
            }
        }

        private Expression VisitTraverseReturns(JObject jobj)
        {
            var rootTypeName = jobj.Property("ModelType").Value.Value<string>();
            var rootType = ModelTypeManager.GetModelType(rootTypeName);
            var returns = new TraverseReturnsExpression(rootType, jobj.Property("Depth").Value.Value<int>(), (ReturnsType)jobj.Property("ReturnsType").Value.Value<int>());
            return returns;
        }

        private Expression VisitTraverseOrigin(JObject jobj)
        {
            var rootTypeName = jobj.Property("ModelType").Value.Value<string>();
            var rootType = ModelTypeManager.GetModelType(rootTypeName);
            var origin = new TraverseOriginExpression(rootType, jobj.Property("Key").Value.Value<string>());
            return origin;
        }

        private Expression VisitPathRootFilter(JObject jobj)
        {
            return new PathRootFilterExpression(_rootType);
        }

        private Expression VisitPathNodeFilterMember(JObject jobj)
        {
            return new PathNodeFilterMemberAccessExpression(_currentNodeType, VisitJObject((JObject)jobj.Property("Predicate").Value));
        }

        private Expression VisitPathEdgeFilterMember(JObject jobj)
        {
            return new PathEdgeFilterMemberAccessExpression(_currentNodeType, VisitJObject((JObject)jobj.Property("Predicate").Value));
        }

        Type _currentEdgeType, _currentNodeType;
        private Expression VisitInEdgeFilter(JObject jobj)
        {
            var modelTypeName = jobj.Property("ModelType").Value.Value<string>();
            var edgeTypeName = jobj.Property("EdgeType").Value.Value<string>();
            var nodeType = _currentNodeType = ModelTypeManager.GetModelType(modelTypeName);
            var edgeType = _currentEdgeType = ModelTypeManager.GetModelType(edgeTypeName);
            var predicate = VisitJObject((JObject)jobj.Property("Predicate").Value);
            var parent = VisitJObject((JObject)jobj.Property("Parent").Value);
            return new InEdgeNodeFilterExpression(edgeType, nodeType, predicate, parent);
        }

        private Expression VisitOutEdgeFilter(JObject jobj)
        {
            var modelTypeName = jobj.Property("ModelType").Value.Value<string>();
            var edgeTypeName = jobj.Property("EdgeType").Value.Value<string>();
            var nodeType =_currentNodeType = ModelTypeManager.GetModelType(modelTypeName);
            var edgeType = _currentEdgeType = ModelTypeManager.GetModelType(edgeTypeName);
            var predicate = VisitJObject((JObject)jobj.Property("Predicate").Value);
            var parent = VisitJObject((JObject)jobj.Property("Parent").Value);
            return new OutEdgeNodeFilterExpression(edgeType, nodeType, predicate, parent);
        }

        Type _rootType;
        private Expression VisitTraverse(JObject jobj)
        {
            var origin = (TraverseOriginExpression)VisitJObject((JObject)jobj.Property("Origin").Value);
            _rootType = origin.Type;
            var traverseType = typeof(IPersistable<>).MakeGenericType(typeof(Path<>).MakeGenericType(origin.Type));
            var exp = new TraverseExpression(traverseType,
                origin, 
                (EdgeNodeFilterExpression)VisitJObject((JObject)jobj.Property("PathFilter").Value), 
                (TraverseReturnsExpression)VisitJObject(jobj.Property("PathSelector").Value.Type == JTokenType.Null ? null : (JObject)jobj.Property("PathSelector").Value));
            return exp;
        }

        private Expression VisitStartsWith(JObject jobj)
        {
            var exp = new StartsWithExpression(jobj.Property("MemberName").Value.Value<string>(), new ScalarExpression(((JValue)jobj.Property("Value").Value).Value?.ToString() ?? "", typeof(object)));
            return exp;
        }

        private Expression VisitOr(JObject jobj)
        {
            return new OrExpression(VisitJObject(jobj.Property("Left").Value as JObject), VisitJObject(jobj.Property("Right").Value as JObject));
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
            var exp = new ContainsExpression(jobj.Property("MemberName").Value.Value<string>(), new ScalarExpression(((JValue)jobj.Property("Value").Value).Value?.ToString() ?? "", typeof(object)));
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

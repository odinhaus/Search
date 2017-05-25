using Altus.Suffūz.Serialization;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Common.DI;
using Common.Serialization;
using Data.Core.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;

namespace Data.Core.Serialization.Json
{
    public class DeleteExpressionSerializer : SerializerBase<DeleteExpression>
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
            var expression = (DeleteExpression)VisitJObject(jobj);

            return expression;
        }

        protected virtual Expression VisitJObject(JObject jobj)
        {
            if (jobj == null) return null;
            var nodeType = (QueryExpressionType)Enum.Parse(typeof(QueryExpressionType), jobj.Property("NodeType").Value.Value<string>());
            switch (nodeType)
            {
                case QueryExpressionType.Delete:
                    {
                        return VisitDelete(jobj);
                    }
                default:
                    {
                        return null;
                    }
            }
        }

        private Expression VisitDelete(JObject jobj)
        {
            var rootTypeName = jobj.Property("ModelType").Value.Value<string>();
            var rootType = ModelTypeManager.GetModelType(rootTypeName);
            var returns = new DeleteExpression(jobj.Property("Model").Value.ToString().FromJson(rootType) as IModel);
            return returns;
        }

        protected override IEnumerable<Type> OnGetSupportedTypes()
        {
            return new Type[] { typeof(DeleteExpression) };
        }

        protected override byte[] OnSerialize(object source)
        {
            return SerializationContext.Instance.TextEncoding.GetBytes(string.Format("{{ NodeType: \"{0}\", ModelType: \"{1}\", Model: {2}}}",
                ((DeleteExpression)source).TypeName,
                ((DeleteExpression)source).ModelType,
                ((DeleteExpression)source).Model.ToJson()));
        }

        protected override bool OnSupportsFormats(string format)
        {
            return StandardFormats.JSON.Equals(format);
        }
    }
}

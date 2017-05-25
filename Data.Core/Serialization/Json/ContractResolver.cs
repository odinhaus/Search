using Altus.Suffūz;
using Altus.Suffūz.Serialization;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Data.Core.Serialization.Json
{
    public class ContractResolver : DefaultContractResolver
    {
        protected override JsonConverter ResolveContractConverter(Type objectType)
        {
            ISerializer serializer = null;
            try
            {
                if (objectType.Implements(typeof(IModel)))
                {
                    serializer = SerializationContext.Instance.GetSerializer<IModel>(StandardFormats.JSON);
                }
                else if (objectType.Implements(typeof(IModelList)))
                {
                    serializer = SerializationContext.Instance.GetSerializer<IModelList>(StandardFormats.JSON);
                }
                else
                {
                    serializer = SerializationContext.Instance.GetSerializer(objectType, StandardFormats.JSON);
                }
            }
            catch { }

            if (serializer != null && serializer is JsonConverter)
            {
                return serializer as JsonConverter;
            }
            else
            {
                return base.ResolveContractConverter(objectType);
            }
        }
    }
}

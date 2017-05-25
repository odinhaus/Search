using Newtonsoft.Json.Serialization;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using System.Linq.Expressions;
using Altus.Suffūz.Serialization;

namespace Common.Serialization.Json
{
    public class ContractResolver : DefaultContractResolver
    {
        protected override JsonConverter ResolveContractConverter(Type objectType)
        {
            ISerializer serializer = null;
            try
            {
                serializer = SerializationContext.Instance.GetSerializer(objectType, StandardFormats.JSON);
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

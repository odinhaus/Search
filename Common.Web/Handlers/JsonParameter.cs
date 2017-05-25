using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using System.ComponentModel;

namespace Common.Web.Handlers
{
    public class JsonParameter
    {
        public string Name { get; set; }
        public string JsonValue { get; set; }
        object _value = null;
        public object Value
        {
            get
            {
                if (_value == null && ValueType != null)
                {
                    if (JsonType == JTokenType.Object || JsonType == JTokenType.Array || (JsonType == JTokenType.String && ValueType != typeof(string)))
                    {
                        IContractResolver resolver = new DefaultContractResolver();
                        try
                        {
                            resolver = AppContext.Current.Container.GetInstance<IContractResolver>();
                        }
                        catch { }
                        _value = JsonConvert.DeserializeObject(JsonValue, ValueType, new JsonSerializerSettings()
                        {
                            ContractResolver = resolver
                        });
                    }
                    else if (JsonType == JTokenType.Null)
                    {
                        _value = null;
                    }
                    else
                    {
                        var converter = TypeDescriptor.GetConverter(ValueType);
                        if (converter.CanConvertFrom(typeof(string)))
                        {
                            _value = converter.ConvertFrom(JsonValue);
                        }
                        else
                        {
                            _value = JsonValue;
                        }
                    }
                    
                }
                return _value;
            }
        }
        public Type ValueType { get; set; }
        public bool IsNamed { get; set; }
        public JTokenType JsonType { get; set; }
    }
}

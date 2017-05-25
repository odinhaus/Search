using Newtonsoft.Json.Linq;
using Common;
using Common.Serialization;
using Data.Core.ComponentModel;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Dynamic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;

namespace Data.Core.Dynamic
{
    public class VirtualResult : DynamicObject
    {
        public VirtualResult(object value)
        {
            this.Value = value;
        }

        public object Value { get; private set; }

        public override bool TryConvert(ConvertBinder binder, out object result)
        {
            var converter = TypeDescriptor.GetConverter(binder.ReturnType.IsArray ? binder.ReturnType.GetElementType() : binder.ReturnType);

            if (converter is ModelTypeConverter)
            {
                ModelTypeConverter.TargetType = binder.ReturnType.IsArray 
                    ? binder.ReturnType.GetElementType() 
                    : binder.ReturnType.Implements<IModel>() 
                        ? binder.ReturnType.GetInterfaces().First(i => i.Implements<IModel>()) 
                        : binder.ReturnType;
            }
            else
            {
                ModelTypeConverter.TargetType = null;
            }

            if (converter.CanConvertFrom(Value.GetType()))
            {
                result = converter.ConvertFrom(Value);
                return true;
            }
            else
            {
                if (Value == null)
                {
                    result = Value;
                }
                else if (Value is JObject)
                {
                    result = ((JObject)Value).ToString().FromJson(ModelTypeConverter.TargetType ?? binder.ReturnType);
                }
                else if (Value is JArray)
                {
                    result = ((JArray)Value).ToString().FromJson(ModelTypeConverter.TargetType ?? binder.ReturnType);
                }
                else if (!TryCast(Value, binder.ReturnType, out result))
                {
                    result = Convert.ChangeType(Value, ModelTypeConverter.TargetType ?? binder.ReturnType);
                }

                ModelTypeConverter.TargetType = null;
                return true;
            }
        }

        public bool TryCast(object value, Type castType, out object castValue)
        {
            var parm = Expression.Parameter(typeof(object));
            var unbox = Expression.Convert(parm, value.GetType());
            var cast = Expression.Convert(unbox, castType);
            var box = Expression.Convert(cast, typeof(object));
            var lambda = Expression.Lambda<Func<object, object>>(box, parm).Compile();
            try
            {
                castValue = lambda(value);
                return true;
            }
            catch
            {
                castValue = null;
                return false;
            }
        }
    }
}

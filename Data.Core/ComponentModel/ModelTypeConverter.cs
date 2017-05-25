using Common;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Globalization;
using Data.Core.Compilation;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace Data.Core.ComponentModel
{
    public class ModelTypeConverter : TypeConverter
    {
        [ThreadStatic]
        private static Type _targetType;

        public static Type ModelBaseType { get; set; }
        public static Type[] AdditionalModelInterfaces { get; set; }
        public static Type TrackedModelBaseType { get; set; }
        public static Type TargetType { get { return _targetType; } set { _targetType = value; } }

        public static Type LinkBaseType { get; set; }
        public static Type[] AdditionalLinkInterfaces { get; set; }

        public override bool CanConvertFrom(ITypeDescriptorContext context, Type sourceType)
        {
            var converter = AppContext.Current?.Container.GetAllInstances<IModelConverter>()
                .FirstOrDefault(c => c.CanConvert(sourceType, TargetType)) ?? null;
            if (converter == null )
            {
                if ((TargetType.Implements(typeof(IModel))) 
                    && (sourceType.Equals(typeof(Dictionary<string, object>)) || sourceType.Equals(typeof(JArray))))
                {
                    return true;
                }
                else
                    return base.CanConvertFrom(context, sourceType);
            }
            else
                return true;
        }

        public override object ConvertFrom(ITypeDescriptorContext context, CultureInfo culture, object value)
        {
            var converter = AppContext.Current?.Container.GetAllInstances<IModelConverter>()
                .FirstOrDefault(c => c.CanConvert(value.GetType(), TargetType)) ?? null;
            if (converter == null)
            {
                if ((TargetType.Implements(typeof(IModel)) ))
                {
                    if (value is Dictionary<string, object>)
                    {
                        return RuntimeModelBuilder.CreateModelInstanceActivator(TargetType, ModelBaseType)((Dictionary<string, object>)value);
                    }
                    else if (value is JArray)
                    {
                        var modelType = RuntimeModelBuilder.CreateModelType(TargetType, ModelBaseType);
                        var modelArrayType = TargetType.MakeArrayType();
                        IContractResolver resolver = new DefaultContractResolver();
                        try
                        {
                            resolver = AppContext.Current.Container.GetInstance<IContractResolver>();
                        }
                        catch { }
                        return JsonConvert.DeserializeObject(((JArray)value).ToString(), modelArrayType, new JsonSerializerSettings()
                        {
                            ContractResolver = resolver
                        });
                    }
                }

                return base.ConvertFrom(context, culture, value);
            }
            else
                return converter.Convert(value); ;
        }
    }
}

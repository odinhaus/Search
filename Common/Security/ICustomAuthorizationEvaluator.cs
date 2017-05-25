using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Common.Security
{
    [TypeConverter(typeof(CustomAuthorizationEvaluatorTypeConverter))]
    public interface ICustomAuthorizationEvaluator
    {
        bool Demand();
    }

    public class CustomAuthorizationEvaluatorTypeConverter : TypeConverter
    {
        public override bool CanConvertFrom(ITypeDescriptorContext context, Type sourceType)
        {
            return sourceType.Equals(typeof(string));
        }

        public override bool CanConvertTo(ITypeDescriptorContext context, Type destinationType)
        {
            return destinationType.Equals(typeof(string));
        }

        public override object ConvertFrom(ITypeDescriptorContext context, CultureInfo culture, object value)
        {
            var type = TypeHelper.CreateType(value.ToString(), new object[0]);
            return type;
        }

        public override object ConvertTo(ITypeDescriptorContext context, CultureInfo culture, object value, Type destinationType)
        {
            return value.GetType().AssemblyQualifiedName;
        }
    }
}

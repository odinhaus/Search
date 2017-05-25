using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Common.Licensing
{
    [AttributeUsage(AttributeTargets.Assembly, AllowMultiple = false, Inherited = false)]
    public class LicenseManagerAttribute : Attribute
    {
        public Type LicenseManagerType { get; set; }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Common.DI
{
    [AttributeUsage(AttributeTargets.Assembly, Inherited = false, AllowMultiple = false)]
    public class PluginAssemblyAttribute : Attribute
    {
        public PluginAssemblyAttribute(string key)
        {
            Key = key;
        }

        public string Key { get; private set; }
    }
}

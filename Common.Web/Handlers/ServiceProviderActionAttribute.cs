using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Common.Web.Handlers
{
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = true, Inherited = true)]
    public class ServiceProviderActionAttribute : Attribute
    {
        public ServiceProviderActionAttribute(string name)
        {
            this.Name = name;
        }

        public string Name { get; private set; }
    }
}

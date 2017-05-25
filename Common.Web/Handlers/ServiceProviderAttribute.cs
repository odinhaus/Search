using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Common.Web.Handlers
{
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = true)]
    public class ServiceProviderAttribute : Attribute
    {
        public ServiceProviderAttribute(string name, Type serviceType)
        {
            this.Name = name;
            this.ServiceType = serviceType;
        }

        public string Name { get; private set; }
        public Type ServiceType { get; private set; }
    }
}

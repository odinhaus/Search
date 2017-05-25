using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Data.Core.Dynamic
{
    public class DynamicFunction<T>
    {
        public DynamicFunction(T target, string instanceName, string functionName, string bodyCS, string references)
        {
            this.TargetInstance = target;
            this.InstanceName = instanceName;
            this.BodyCS = bodyCS;
            this.Name = functionName;
            this.References = references;
        }

        public T TargetInstance { get; private set; }
        public string InstanceName { get; private set; }
        public string BodyCS { get; private set; }
        public string Name { get; private set; }
        public string References { get; private set; }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Common.DI
{
    public class ContainerMapping : IContainerMapping
    {
        public Type TargetType { get; private set; }
        public Type ImplementationType { get; private set; }
        public object ImplementationObject { get; private set; }
        public string InstanceKey { get; private set; }

        public void Map<T>(T instance) where T : class
        {
            this.TargetType = typeof(T);
            this.ImplementationObject = instance;
            this.ImplementationType = instance.GetType();
            this.InstanceKey = this.TargetType.FullName;
        }

        public void Map<T>(T instance, string key) where T : class
        {
            this.TargetType = typeof(T);
            this.ImplementationObject = instance;
            this.ImplementationType = instance.GetType();
            this.InstanceKey = key;
        }

        public void Map<T, U>() where U : T
        {
            this.TargetType = typeof(T);
            this.ImplementationObject = null;
            this.ImplementationType = typeof(U);
            this.InstanceKey = this.TargetType.FullName;
        }
    }
}

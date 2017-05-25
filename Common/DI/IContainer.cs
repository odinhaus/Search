using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Common.DI
{
    public delegate void ContainerChangedHandler(object sender, ContainerChangedEventArgs e);

    public enum ContainerUpdateType
    {
        Add,
        Remove,
        Rename
    }

    public class ContainerChangedEventArgs : EventArgs
    {
        public ContainerChangedEventArgs(ContainerUpdateType type, Type serviceType, Type implmentationType)
            :this(type, serviceType, implmentationType, null)
        {

        }

        public ContainerChangedEventArgs(ContainerUpdateType type, Type serviceType, Type implmentationType, string key)
        {
            this.Key = key;
            this.ServiceType = serviceType;
            this.ImplementationType = implmentationType;
            this.UpdateType = type;
        }

        public string Key { get; private set; }
        public Type ServiceType { get; private set; }
        public Type ImplementationType { get; private set; }
        public ContainerUpdateType UpdateType { get; private set; }
    }

    public interface IContainer
    {
        event ContainerChangedHandler ContainerChanged;

        IEnumerable<T> GetAllInstances<T>(params object[] ctorArgs);
        IEnumerable GetAllInstances(Type pluginType, params object[] ctorArgs);
        T GetInstance<T>(params object[] ctorArgs);
        T GetInstance<T>(string instanceKey, params object[] ctorArgs);
        object GetInstance(Type pluginType, params object[] ctorArgs);
        object GetInstance(Type pluginType, string instanceKey, params object[] ctorArgs);
        void Map(Type sourceType, Type targetType);
        void Map(object sourceInstance, Type targetType);
        void Map<T, U>() where U : T;
        void Map<T>(T instance) where T : class;
        void Map<T>(T instance, string key) where T : class;
    }
}

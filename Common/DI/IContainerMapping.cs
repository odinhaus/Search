using System;

namespace Common.DI
{
    public interface IContainerMapping
    {
        Type TargetType { get; }
        Type ImplementationType { get;  }
        object ImplementationObject { get; }
        string InstanceKey { get;  }
        void Map<T>(T instance) where T : class;
        void Map<T>(T instance, string key) where T : class;
        void Map<T, U>() where U : T;
    }
}
using Common.DI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Common.Application
{
    public interface IInitialize
    {
        void Initialize(string name, params string[] args);
        void Register(IContainerMappings containerMappings);
        bool IsInitialized { get; }
        bool IsRegistered { get; }
        bool IsEnabled { get; }
    }
}

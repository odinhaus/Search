using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Common.DI
{
    public class NoPluginLoader : IPluginLoader
    {
        public bool IsComplete
        {
            get
            {
                return true;
            }
        }

        public event PluginLoadBeginHandler LoadBegin;
        public event PluginLoadCompleteHandler LoadComplete;
        public event PluginLoadCoreBegin LoadCoreBegin;
        public event PluginLoadCoreComplete LoadCoreComplete;
        public event PluginLoadExtensionsBegin LoadExtensionsBegin;
        public event PluginLoadExtensionsComplete LoadExtensionsComplete;
        public event PluginLoadStatusHandler LoadStatus;

        public IContainer Container { get; private set; }

        public void Add(Assembly assembly)
        {
        }

        public void Add(object plugin)
        {
        }

        public void Add(object plugin, string name)
        {
        }

        public void Cancel()
        {
        }

        public void LoadPlugins(params string[] args)
        {
            if (this.LoadComplete != null)
            {
                this.LoadComplete();
            }
        }

        public void SetContainer(IContainer container)
        {
            this.Container = container;
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Common.DI
{
    public delegate void PluginLoadStatusHandler(PluginLoadStatusEventArgs e);
    public delegate void PluginLoadCompleteHandler();
    public delegate void PluginLoadBeginHandler();
    public delegate void PluginLoadCoreBegin();
    public delegate void PluginLoadCoreComplete();
    public delegate void PluginLoadExtensionsBegin();
    public delegate void PluginLoadExtensionsComplete();

    public class PluginLoadStatusEventArgs : EventArgs
    {
        public PluginLoadStatusEventArgs() { }
        public PluginLoadStatusEventArgs(string message, object plugin, int index, int count)
        {
            this.Message = message;
            this.Plugin = plugin;
            this.Name = plugin.GetType().FullName;
            this.Index = index;
            this.Count = count;
        }
        public PluginLoadStatusEventArgs(string message, object plugin, string name, int index, int count)
        {
            this.Message = message;
            this.Plugin = plugin;
            this.Name = name;
            this.Count = count;
            this.Index = index;
        }
        public string Message { get; private set; }
        public object Plugin { get; private set; }
        public string Name { get; private set; }
        public int Index { get; private set; }
        public int Count { get; private set; }
    }

    public interface IPluginLoader
    {
        /// <summary>
        /// Attach to this method to receive status updates during the module load process
        /// </summary>
        event PluginLoadStatusHandler LoadStatus;
        /// <summary>
        /// Attach to this event to receive notification that the modules have all been loaded
        /// </summary>
        event PluginLoadCompleteHandler LoadComplete;
        /// <summary>
        /// Attach to this event to receive notification that a component loading session is about to begin
        /// </summary>
        event PluginLoadBeginHandler LoadBegin;
        /// <summary>
        /// Attach to this event to receive notification that the loading of core framework assemblies has started
        /// </summary>
        event PluginLoadCoreBegin LoadCoreBegin;
        /// <summary>
        /// Attach to this event to receive notification that the loading of core framework assemblies has completed
        /// </summary>
        event PluginLoadCoreComplete LoadCoreComplete;
        /// <summary>
        /// Attach to this event to receive notification that the loading of core framework assemblies has started
        /// </summary>
        event PluginLoadExtensionsBegin LoadExtensionsBegin;
        /// <summary>
        /// Attach to this event to receive notification that the loading of core framework assemblies has completed
        /// </summary>
        event PluginLoadExtensionsComplete LoadExtensionsComplete;
        /// <summary>
        /// Sets the component container to use to store and resolve container references
        /// </summary>
        /// <param name="container"></param>
        void SetContainer(IContainer container);
        /// <summary>
        /// Call this method to begin the load process
        /// </summary>
        /// <param name="targetRegion">provide the region the modules should be loaded into</param>
        void LoadPlugins(params string[] args);
        /// <summary>
        /// gets a boolean indicating whether the module loader has completed the load operation
        /// </summary>
        bool IsComplete { get; }
        /// <summary>
        /// call this method to cancel the load operation
        /// </summary>
        void Cancel();
    }
}

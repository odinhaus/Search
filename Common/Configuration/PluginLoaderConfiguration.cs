using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Common.Configuration
{
    [Serializable]
    public class PluginLoaderConfiguration : ConfigurationSection
    {
        [ConfigurationProperty("coreAssemblies", IsDefaultCollection = false)]
        [ConfigurationCollection(typeof(CoreAssemblyCollection), AddItemName = "add")]
        public CoreAssemblyCollection CoreAssemblies
        {
            get
            {
                return (CoreAssemblyCollection)this["coreAssemblies"];
            }
            set
            {
                this["coreAssemblies"] = value;
            }
        }

        [ConfigurationProperty("discoveryPaths", IsDefaultCollection = false)]
        [ConfigurationCollection(typeof(DiscoveryPathCollection), AddItemName = "add")]
        public DiscoveryPathCollection DiscoveryPaths
        {
            get
            {
                return (DiscoveryPathCollection)this["discoveryPaths"];
            }
            set
            {
                this["discoveryPaths"] = value;
            }
        }
    }
}

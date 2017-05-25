using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Common.Configuration
{
    [Serializable]
    public class DiscoveryPathElement : ConfigurationElement, IComparable<DiscoveryPathElement>
    {
        [ConfigurationProperty("path", IsRequired = true)]
        public string Path
        {
            get
            {
                return (string)this["path"];
            }
            set
            {
                this["path"] = value;
            }
        }

        [ConfigurationProperty("recurse", IsRequired = true)]
        public bool Recurse
        {
            get
            {
                return (bool)this["recurse"];
            }
            set
            {
                this["recurse"] = value;
            }
        }

        public string CodeBase
        {
            get
            {
                if (System.IO.Path.IsPathRooted(this.Path))
                {
                    return this.Path;
                }
                else
                {
                    return System.IO.Path.Combine(AppContext.Current.CodeBase, this.Path);
                }
            }
        }

        public bool IsValid { get; set; }

        public int CompareTo(DiscoveryPathElement other)
        {
            return this.Path.CompareTo(other.Path);
        }

        public override string ToString()
        {
            return Path;
        }
    }
}

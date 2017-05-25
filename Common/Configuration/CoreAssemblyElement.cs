using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;

namespace Common.Configuration
{
    [Serializable]
    public class CoreAssemblyElement : ConfigurationElement, IComparable<CoreAssemblyElement>
    {
        [ConfigurationProperty("assembly", IsRequired = true)]
        public string Assembly
        {
            get
            {
                return (string)this["assembly"];
            }
            set
            {
                this["assembly"] = value;
            }
        }

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

        [ConfigurationProperty("priority", IsRequired = true)]
        public int Priority
        {
            get
            {
                return (int)this["priority"];
            }
            set
            {
                this["priority"] = value;
            }
        }
        [XmlIgnore]
        public string CodeBase
        {
            get
            {
                if (System.IO.Path.IsPathRooted(this.Path))
                {
                    return System.IO.Path.Combine(this.Path, this.Assembly);
                }
                else
                {
                    return System.IO.Path.Combine(System.IO.Path.Combine(AppContext.Current.CodeBase, this.Path), this.Assembly);
                }
            }
        }
        [XmlIgnore]
        public bool IsValid { get; set; }
        [XmlIgnore]
        public Assembly LoadedAssembly { get; set; }

        public int CompareTo(CoreAssemblyElement other)
        {
            return this.Priority.CompareTo(other.Priority);
        }

        public override string ToString()
        {
            return Assembly;
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;

namespace Common.Configuration
{
    [Serializable]
    [XmlRoot("Target")]
    public class DiscoveryTarget
    {
        public DiscoveryTarget() { Files = new DiscoveryFileCollection(); }
        [XmlAttribute]
        public string Product { get; set; }
        [XmlArray("Files")]
        [XmlArrayItem(ElementName = "File", Type = typeof(DiscoveryFileElement))]
        public DiscoveryFileCollection Files { get; set; }

        public bool IsLocal
        {
            get
            {
                return Product != null
                    && Product.Equals(AppContext.GetEnvironmentVariable<string>("ProductName", ""), StringComparison.InvariantCultureIgnoreCase);
            }
        }

        public override string ToString()
        {
            return Product;
        }
    }
}

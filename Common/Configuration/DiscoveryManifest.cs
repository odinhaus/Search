using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;

namespace Common.Configuration
{
    [Serializable]
    [XmlRoot(ElementName = "Manifest")]
    public class DiscoveryManifest
    {
        public DiscoveryManifest() { Targets = new DiscoveryManifestTargetCollection(); }
        [XmlArray("Targets")]
        [XmlArrayItem(ElementName = "Target", Type = typeof(DiscoveryTarget))]
        public DiscoveryManifestTargetCollection Targets { get; set; }
        [XmlAttribute]
        public DateTime LastUpdated { get; set; }
        [XmlIgnore]
        public string LocalPath { get; set; }

        public void Save()
        {
            if (string.IsNullOrEmpty(LocalPath)) throw new InvalidOperationException("Manifest LocalPath must be set prior to calling Save.");

            XmlSerializer serializer = new XmlSerializer(typeof(DiscoveryManifest));

            using (StreamWriter sw = new StreamWriter(LocalPath))
            {
                serializer.Serialize(sw, this);
            }
        }
    }
}

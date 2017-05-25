using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;

namespace Common.Configuration
{
    [Serializable]
    [XmlRoot("File")]
    public class DiscoveryFileElement
    {
        [XmlAttribute]
        public string Name { get; set; }
        [XmlAttribute]
        public bool Reflect { get; set; }
        [XmlAttribute]
        public bool IsPrimary { get; set; }
        [XmlAttribute]
        public bool IsAppConfig { get; set; }
        [XmlAttribute]
        public bool IsDatabase { get; set; }
        [XmlAttribute]
        public string Source { get; set; }
        [XmlAttribute]
        public string Destination { get; set; }
        [XmlAttribute]
        public string Checksum { get; set; }
        [XmlIgnore]
        public string CodeBase { get; set; }
        [XmlIgnore]
        public bool IsValid { get; set; }
        [XmlIgnore]
        public Assembly LoadedAssembly { get; set; }
        [XmlIgnore]
        public bool Exists { get { return File.Exists(CodeBase); } }

        public override string ToString()
        {
            return Name;
        }
    }
}

using Altus.Suffūz.Serialization.Binary;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;

namespace Common.Application
{
    [Serializable]
    [XmlRoot("File")]
    public class AppFile
    {
        [XmlElement()]
        [BinarySerializable(1)]
        public string Name { get; set; }
        [XmlElement()]
        [BinarySerializable(2)]
        public byte[] Data { get; set; }
        [XmlElement()]
        [BinarySerializable(3)]
        public string Checksum { get; set; }
        [XmlElement()]
        [BinarySerializable(4)]
        public string CodeBase { get; set; }
    }
}

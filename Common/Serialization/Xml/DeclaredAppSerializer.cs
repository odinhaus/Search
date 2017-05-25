using Altus.Suffūz.Serialization;
using Common.Application;
using Common.IO;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;
using Common.DI;

namespace Common.Serialization.Xml
{
    public class DeclaredAppSerializer : SerializerBase, ISerializer<DeclaredApp>
    {
        protected override IEnumerable<Type> OnGetSupportedTypes()
        {
            return new Type[] { typeof(DeclaredApp) };
        }

        protected override byte[] OnSerialize(object source)
        {
            XmlSerializer serializer = new XmlSerializer(source.GetType());
            using (MemoryStream ms = new MemoryStream())
            {
                serializer.Serialize(ms, source);
                return ms.ToArray();
            }
        }

        protected override object OnDeserialize(byte[] source, Type targetType)
        {
            XmlSerializer serializer = new XmlSerializer(targetType);
            using (MemoryStream ms = new MemoryStream(source))
            {
                return serializer.Deserialize(ms);
            }
        }

        protected override bool OnSupportsFormats(string format)
        {
            return format.Equals(StandardFormats.XML, StringComparison.InvariantCultureIgnoreCase);
        }

        public byte[] Serialize(DeclaredApp source)
        {
            return OnSerialize(source);
        }

        public void Serialize(DeclaredApp source, Stream outputStream)
        {
            using (var bw = new BinaryWriter(outputStream))
            {
                bw.Write(Serialize(source));
            }
        }

        public DeclaredApp Deserialize(byte[] source)
        {
            return (DeclaredApp)base.Deserialize(source, typeof(DeclaredApp));
        }

        public DeclaredApp Deserialize(Stream inputSource)
        {
            return Deserialize(StreamHelper.GetBytes(inputSource));
        }

        public override void Register(IContainerMappings mappings)
        {
            mappings.Add().Map<ISerializer<DeclaredApp>, DeclaredAppSerializer>();
        }
    }
}

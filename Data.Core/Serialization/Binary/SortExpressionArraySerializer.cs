using Altus.Suffūz.Serialization;
using Common.IO;
using Data.Core.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Data.Core.Serialization.Binary
{
    public class SortExpressionArraySerializer : ISerializer<SortExpression[]>
    {
        public bool IsScalar
        {
            get
            {
                return false;
            }
        }

        public int Priority
        {
            get
            {
                return 0;
            }
        }

        public SortExpression[] Deserialize(Stream inputSource)
        {
            return Deserialize(StreamHelper.GetBytes(inputSource));
        }

        public SortExpression[] Deserialize(byte[] source)
        {
            using (var ms = new MemoryStream(source))
            {
                using (var br = new BinaryReader(ms))
                {
                    var result = new SortExpression[br.ReadInt32()];
                    for (int i = 0; i < result.Length; i++)
                    {
                        result[i] = new SortExpression();
                        result[i].FromBytes(br.ReadBytes(br.ReadInt32()));
                    }
                    return result;
                }
            }
        }

        public object Deserialize(byte[] source, Type targetType)
        {
            return Deserialize(source);
        }

        public byte[] Serialize(SortExpression[] source)
        {
            return Serialize((object)source);
        }

        public byte[] Serialize(object source)
        {
            using (var ms = new MemoryStream())
            {
                using(var bw = new BinaryWriter(ms))
                {
                    bw.Write(((SortExpression[])source).Length);
                    foreach(var se in (SortExpression[])source)
                    {
                        var bytes = se.ToBytes();
                        bw.Write(bytes.Length);
                        bw.Write(bytes);
                    }

                    return ms.ToArray();
                }
            }
        }

        public void Serialize(SortExpression[] source, Stream outputStream)
        {
            StreamHelper.Write(outputStream, Serialize(source));
        }

        public bool SupportsFormat(string format)
        {
            return format.Equals(StandardFormats.BINARY);
        }

        public bool SupportsType(Type type)
        {
            return type == typeof(SortExpression[]);
        }
    }
}

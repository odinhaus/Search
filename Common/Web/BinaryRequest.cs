using Common.Serialization.Binary;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Collections;
using System.IO;
using Altus.Suffūz.Serialization;

namespace Common.Web
{
    public class BinaryRequest : IEnumerable<KeyValuePair<string, object>>, IBinarySerializable
    {
        protected List<KeyValuePair<string, object>> _args = new List<KeyValuePair<string, object>>();

        public BinaryRequest(IEnumerable<KeyValuePair<string, object>> args)
        {
            foreach (var arg in args)
                _args.Add(arg);
        }

        public byte[] ProtocolBuffer
        {
            get;

            set;
        }


        public IEnumerator<KeyValuePair<string, object>> GetEnumerator()
        {
            return _args.GetEnumerator();
        }

        public virtual byte[] ToBytes()
        {
            using (var ms = new MemoryStream())
            {
                using (var bw = new BinaryWriter(ms))
                {
                    bw.Write(_args.Count);

                    foreach(var arg in _args)
                    {
                        bw.Write(arg.Key);
                        bw.Write(arg.Value.GetType().AssemblyQualifiedName);
                        if (arg.Value is IBinarySerializable)
                        {
                            bw.Write(true);
                            var bytes = ((IBinarySerializable)arg.Value).ToBytes();
                            bw.Write(bytes.Length);
                            bw.Write(bytes);
                        }
                        else
                        {
                            bw.Write(false);
                            var serializer = SerializationContext.Instance.GetSerializer(arg.Value.GetType(), StandardFormats.BINARY);
                            var bytes = serializer.Serialize(arg.Value);
                            bw.Write(bytes.Length);
                            bw.Write(bytes);
                        }
                    }

                    return ms.ToArray();
                }
            }
        }

        public virtual void FromBytes(byte[] source)
        {
            using (var ms = new MemoryStream(source))
            {
                using (var br = new BinaryReader(ms))
                {
                    this._args = new List<KeyValuePair<string, object>>();
                    var count = br.ReadInt32();
                    for(int i = 0; i < count; i++)
                    {
                        var name = br.ReadString();
                        var typeName = br.ReadString();
                        var type = TypeHelper.GetType(typeName);
                        var isBinarySerializable = br.ReadBoolean();
                        if (isBinarySerializable)
                        {
                            var item = Activator.CreateInstance(type) as IBinarySerializable;
                            item.FromBytes(br.ReadBytes(br.ReadInt32()));
                            this._args.Add(new KeyValuePair<string, object>(name, item));
                        }
                        else
                        {
                            var serializer = SerializationContext.Instance.GetSerializer(type, StandardFormats.BINARY);
                            var item = serializer.Deserialize(br.ReadBytes(br.ReadInt32()), type);
                            this._args.Add(new KeyValuePair<string, object>(name, item));
                        }
                    }
                }
            }
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return this.GetEnumerator();
        }
    }
}

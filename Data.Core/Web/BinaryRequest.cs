using Altus.Suffūz.Serialization;
using Common;
using Common.Serialization.Binary;
using Data.Core;
using Data.Core.Compilation;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Data.Core.Web
{
    public class BinaryRequest : Common.Web.BinaryRequest
    {
        public BinaryRequest(IEnumerable<KeyValuePair<string, object>> args) : base(args) { }

        public override byte[] ToBytes()
        {
            using (var ms = new MemoryStream())
            {
                using (var bw = new BinaryWriter(ms))
                {
                    bw.Write(_args.Count);

                    foreach (var arg in _args)
                    {
                        bw.Write(arg.Key);
                        if (arg.Value is IModel)
                        {
                            bw.Write(true);
                            bw.Write(ModelTypeManager.GetModelName(((IModel)arg.Value).ModelType));
                        }
                        else
                        {
                            bw.Write(false);
                            bw.Write(arg.Value?.GetType().AssemblyQualifiedName ?? "null");
                        }

                        
                        if (arg.Value is IBinarySerializable)
                        {
                            bw.Write(true);
                            var bytes = ((IBinarySerializable)arg.Value).ToBytes();
                            bw.Write(bytes.Length);
                            bw.Write(bytes);
                        }
                        else if (arg.Value == null)
                        {
                            bw.Write(false);
                            bw.Write(0);
                            bw.Write(new byte[0]);
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

        public override void FromBytes(byte[] source)
        {
            using (var ms = new MemoryStream(source))
            {
                using (var br = new BinaryReader(ms))
                {
                    this._args = new List<KeyValuePair<string, object>>();
                    var count = br.ReadInt32();
                    for (int i = 0; i < count; i++)
                    {
                        var name = br.ReadString();
                        var isModelType = br.ReadBoolean();
                        var typeName = br.ReadString();
                        if (typeName == "null")
                        {
                            this._args.Add(new KeyValuePair<string, object>(name, null));
                            br.ReadBoolean();
                            br.ReadInt32();
                            br.ReadBytes(0);
                        }
                        else
                        {
                            var type = isModelType ? ModelTypeManager.GetModelType(typeName) : TypeHelper.GetType(typeName);
                            var isBinarySerializable = br.ReadBoolean();
                            if (isBinarySerializable)
                            {
                                var item = (isModelType ? RuntimeModelBuilder.CreateModelInstance(type) : Activator.CreateInstance(type)) as IBinarySerializable;
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
        }
    }
}

using Altus.Suffūz.Serialization.Binary;
using Common.Serialization.Binary;
using Data.Core.Compilation;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Data.Core
{
    public class LockedModel<T> : IBinarySerializable where T : IModel
    {
        [BinarySerializable(0)]
        public T Model { get; set; }
        [BinarySerializable(1)]
        public ILock Lock { get; set; }
        [BinarySerializable(2)]
        public byte[] ProtocolBuffer
        {
            get;
            set;
        }

        public byte[] ToBytes()
        {
            using (var ms = new MemoryStream())
            {
                using (var bw = new BinaryWriter(ms))
                {
                    bw.Write(ModelTypeManager.GetModelName(Model.ModelType));
                    var bytes = ((IBinarySerializable)Model).ToBytes();
                    bw.Write(bytes.Length);
                    bw.Write(bytes);
                    bytes = ((IBinarySerializable)Lock).ToBytes();
                    bw.Write(bytes.Length);
                    bw.Write(bytes);
                    return ms.ToArray();
                }
            }
        }

        public void FromBytes(byte[] source)
        {
            using (var ms = new MemoryStream(source))
            {
                using (var br = new BinaryReader(ms))
                {
                    var modelName = br.ReadString();
                    var model = (IModel)RuntimeModelBuilder.CreateModelInstance(ModelTypeManager.GetModelType(modelName));
                    ((IBinarySerializable)model).FromBytes(br.ReadBytes(br.ReadInt32()));
                    this.Model = (T)model;
                    Lock = (ILock)RuntimeModelBuilder.CreateModelInstance<ILock>();
                    ((IBinarySerializable)Lock).FromBytes(br.ReadBytes(br.ReadInt32()));
                }
            }
        }
    }
}

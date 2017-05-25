using Altus.Suffūz.Serialization.Binary;
using Common.Serialization.Binary;
using Data.Core.Compilation;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Data.Core.Domains.Operations
{
    public class QueuedModel<T> : IBinarySerializable where T : IModel
    {
        public QueuedModel() { }
        public QueuedModel(T model, int rank)
        {
            Model = model;
            Rank = rank;
        }
        [BinarySerializable(0)]
        public T Model { get; set; }
        [BinarySerializable(1)]
        public int Rank { get; private set; }
        [BinarySerializable(2)]
        public int ForcedRank { get; set; }
        [BinarySerializable(3)]
        public bool IsOnHold { get; set; }
        [BinarySerializable(5)]
        public byte[] ProtocolBuffer
        {
            get;
            set;
        }

        public static implicit operator T(QueuedModel<T> queuedModel)
        {
            return queuedModel.Model;
        }

        public byte[] ToBytes()
        {
            using (var ms = new MemoryStream())
            {
                using (var bw = new BinaryWriter(ms))
                {
                    bw.Write(Model != null);
                    bw.Write(ModelTypeManager.GetModelName(Model.ModelType));
                    var bytes = ((IBinarySerializable)Model).ToBytes();
                    bw.Write(bytes.Length);
                    bw.Write(bytes);
                    bw.Write(Rank);
                    bw.Write(ForcedRank);
                    bw.Write(IsOnHold);
                }
                return ms.ToArray();
            }
        }

        public void FromBytes(byte[] source)
        {
            using (var ms = new MemoryStream(source))
            {
                using (var br = new BinaryReader(ms))
                {
                    if (br.ReadBoolean())
                    {
                        var model = (T)RuntimeModelBuilder.CreateModelInstance(ModelTypeManager.GetModelType(br.ReadString()));
                        ((IBinarySerializable)model).FromBytes(br.ReadBytes(br.ReadInt32()));
                        this.Model = model;
                    }
                    Rank = br.ReadInt32();
                    ForcedRank = br.ReadInt32();
                    IsOnHold = br.ReadBoolean();
                }
            }
        }
    }
}

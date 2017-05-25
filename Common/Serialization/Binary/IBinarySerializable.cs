using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Common.Serialization.Binary
{
    public interface IBinarySerializable
    {
        byte[] ToBytes();
        void FromBytes(byte[] source);
        byte[] ProtocolBuffer { get; set; }
    }
}

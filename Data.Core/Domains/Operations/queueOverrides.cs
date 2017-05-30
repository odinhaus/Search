using Altus.Suffūz.Serialization.Binary;
using Common.Security;
using Data.Core.ComponentModel;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Data.Core.Domains.Operations
{
    [TypeConverter(typeof(ModelTypeConverter))]
    [Model("queueOverrides")]
    public interface queueOverrides : ILink<long>
    {
        [BinarySerializable(10)]
        int ForcedRank { get; set; }
        [BinarySerializable(11)]
        bool IsOnHold { get; set; }
    }
}

using Altus.Suffūz.Serialization.Binary;
using Data.Core.ComponentModel;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Data.Core
{
    [TypeConverter(typeof(ModelTypeConverter))]
    [Model("lock")]
    public interface ILock : ILink
    {
        [Searchable]
        [BinarySerializable(10)]
        DateTime Expires { get; set; }
        [BinarySerializable(11)]
        bool IsExtension { get; set; }
    }
}

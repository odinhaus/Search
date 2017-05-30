using Altus.Suffūz.Serialization.Binary;
using Common.Security;
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
    [Model("Directory")]
    public interface IDirectory : IModel<long>
    {
        [Searchable]
        [BinarySerializable(23)]
        string Parent { get; set; }
        [Searchable]
        [BinarySerializable(28)]
        string FullName { get; set; }
        [Searchable]
        [BinarySerializable(33)]
        string Name { get; set; }
        [Searchable]
        [BinarySerializable(34)]
        string ExternalKey { get; set; }
        [BinarySerializable(35)]
        int Attributes { get; set; }
        [Searchable]
        [BinarySerializable(36)]
        string Root { get; set; }
        [BinarySerializable(37)]
        DateTime LastAccessTime { get; set; }
    }
}

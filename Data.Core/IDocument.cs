
using Altus.Suffūz.Serialization.Binary;
using Common.Security;
using Data.Core.ComponentModel;
using System;
using System.ComponentModel;

namespace Data.Core
{
    [TypeConverter(typeof(ModelTypeConverter))]
    [Model("Document", "Shs")]
    public interface IDocument : IModel<long>
    {
        [Searchable]
        [BinarySerializable(21)]
        IDocumentMetaData MetaData { get; set; }
        [Searchable]
        [BinarySerializable(22)]
        long Length { get; set; }
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
        [BinarySerializable(36)]
        DateTime LastAccessTime { get; set; }
    }

    [TypeConverter(typeof(ModelTypeConverter))]
    public interface IDocumentMetaData : ISubModel
    {
        [Searchable]
        [BinarySerializable(20)]
        string DocumentType { get; set; }
        [Searchable]
        [BinarySerializable(21)]
        string Description { get; set; }
        [BinarySerializable(24)]
        string ExtraInfo { get; set; }
    }
}

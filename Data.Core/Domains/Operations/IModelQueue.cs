using Altus.Suffūz.Serialization.Binary;
using Common.Collections;
using Data.Core;
using Data.Core.ComponentModel;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Data.Core.Domains.Operations
{
    public interface IModelQueue : IModel<long>
    {
        [BinarySerializable(10)]
        string Name { get; set; }
        [BinarySerializable(11)]
        string Description { get; set; }
        [BinarySerializable(12)]
        bool IsActive { get; set; }
        [BinarySerializable(13)]
        Flock<IModelQueueQuery> Queries { get; set; }
    }

    [TypeConverter(typeof(ModelTypeConverter))]
    public interface IModelQueueQuery : ISubModel
    {
        [BinarySerializable(10)]
        string Query { get; set; }
        [BinarySerializable(11)]
        int Rank { get; set; }
    }
}

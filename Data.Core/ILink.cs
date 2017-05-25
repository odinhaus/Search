using Altus.Suffūz.Serialization.Binary;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Data.Core
{
    public interface ILink : IModel
    {
        [BinarySerializable(10)]
        IModel To { get; set; }
        [BinarySerializable(11)]
        IModel From { get; set; }
    }

    public interface ILink<T> : IModel<T>, ILink
    {
    }
}

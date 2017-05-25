using Data.Core.Auditing;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Data.Core
{
    public interface IModel : INotifyPropertyChanged, INotifyPropertyChanging
    {
        Type ModelType { get; }
        string GetKey();
        void SetKey(string value);
        [Altus.Suffūz.Serialization.Binary.BinarySerializable(1)]
        bool IsDeleted { get; set; }
        bool IsNew { get; }
        [Altus.Suffūz.Serialization.Binary.BinarySerializable(2)]
        DateTime Created { get; set; }
        [Altus.Suffūz.Serialization.Binary.BinarySerializable(3)]
        DateTime Modified { get; set; }
        IEnumerable<AuditedChange> Compare(IModel model, string prefix);
    }

    public interface IModel<T> : IModel
    {
        [Altus.Suffūz.Serialization.Binary.BinarySerializable(0)]
        [Searchable]
        T Key { get; set; }

        
    }
}

using Altus.Suffūz.Serialization.Binary;
using Common.Collections;
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
    [Model("UserPreferences", "System")]
    public interface IUserPreferences : IModel<long>
    {
        [Searchable]
        [BinarySerializable(10)]
        string UserName { get; set; }
        [BinarySerializable(20)]
        Flock<ISavedBlade> SavedBlades { get; set; }
        [BinarySerializable(30)]
        bool SavePinnedBladesOnly { get; set; }
        [BinarySerializable(40)]
        Flock<ISavedBlade> FavoredBlades { get; set; }
    }

    [Model("SavedBlade", "System.UserPreferences")]
    public interface ISavedBlade : ISubModel
    {
        [Searchable]
        [BinarySerializable(10)]
        string BladePath { get; set; }
        [Searchable]
        [BinarySerializable(20)]
        string BladeKey { get; set; }
        [BinarySerializable(30)]
        byte[] Thumbnail { get; set; }
        [BinarySerializable(40)]
        byte[] Icon { get; set; }
    }

}

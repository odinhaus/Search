using Altus.Suffūz.Serialization.Binary;
using Common.Security;
using Data.Core;
using Data.Core.ComponentModel;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Data.Core.Security
{
    [TypeConverter(typeof(ModelTypeConverter))]
    [Model("UserGroup")]
    public interface IUserGroup : IModel<long>
    {
        [BinarySerializable(10)]
        string Name { get; set; }
        [BinarySerializable(11)]
        long[] UserIds { get; set; }
    }
}

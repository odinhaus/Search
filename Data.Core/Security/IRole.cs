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

namespace Data.Core.Security
{
    [TypeConverter(typeof(ModelTypeConverter))]
    [Model("Role")]
    public interface IRole : IModel<long>
    {
        [BinarySerializable(10)]
        string Name { get; set; }
    }

    public class IRoleDefaults
    {
        public const string AdministratorsRoleName = "Admin";
        public const string UsersRoleName = "Users";
        public static IRole AdministratorsRole;
        public static IRole UsersRole;
    }
}

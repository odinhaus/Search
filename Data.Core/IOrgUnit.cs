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
    [Model("OrgUnit")]
    public interface IOrgUnit : IModel<long>, INamedModel
    {
        [BinarySerializable(10)]
        [Searchable]
        string Type { get; set; }
        [BinarySerializable(11)]
        [Searchable]
        string Prefix { get; set; }
    }

    public class OrgUnitTypes
    {
        public const string Customer = "Customer";

        private OrgUnitTypes() { }

    }


    public class IOrgUnitDefaults
    {
        public const string RootOrgUnitName = "Root";
        public static IOrgUnit RootOrgUnit;
    }

}

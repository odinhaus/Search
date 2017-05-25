using Common.Security;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Data.Core
{
    [Model("Container")]
    public interface IContainer : IModel<long>, INamedModel
    {
    }

    public class IContainerDefaults
    {
        public const string DefaultContainerSuffix = "_Default";

        public static string DefaultContainer(IOrgUnit orgUnit)
        {
            return orgUnit.Name + DefaultContainerSuffix;
        }
    }
}

using Common.Application;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Common.Licensing
{
    public interface ILicensedInstaller : IInstaller, ILicensedPlugin
    {
        IEnumerable<DeclaredApp> Apps
        {
            get;
        }
    }
}

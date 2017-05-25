using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Common.Application;
using Common.DI;

namespace Common.Licensing
{
    public class NoLicensedAppInstaller : ILicensedInstaller
    {
        public IEnumerable<DeclaredApp> Apps
        {
            get
            {
                return new DeclaredApp[0];
            }
        }

        public bool IsEnabled
        {
            get
            {
                return true;
            }
        }

        public bool IsInitialized
        {
            get
            {
                return true;
            }
        }

        public bool IsInstalled
        {
            get
            {
                return true;
            }
        }
        public bool IsRegistered
        {
            get;
            protected set;
        }


        public void ApplyLicensing(ILicense[] licenses, params string[] args)
        {
        }

        public void Initialize(string name, params string[] args)
        {
        }

        public void Install(params string[] appNames)
        {
        }

        public bool IsLicensed(object plugin)
        {
            return true;
        }

        public void Register(IContainerMappings mappings)
        {
            mappings.Add().Map<ILicensedInstaller>(this);
            this.IsRegistered = true;
        }
    }
}

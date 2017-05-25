using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Common.DI;

namespace Common.Licensing
{
    public class NoLicenseManager : ILicenseManager
    {
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

        public bool IsRegistered
        {
            get;
            protected set;
        }

        public ILicense[] GetLicenses()
        {
            return new ILicense[0];
        }

        public void Initialize(string name, params string[] args)
        {
        }

        public void Register(IContainerMappings mappings)
        {
            mappings.Add().Map<ILicenseManager>(this);
            this.IsRegistered = true;
        }
    }
}

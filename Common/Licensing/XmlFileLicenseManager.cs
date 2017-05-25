using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Common.DI;

namespace Common.Licensing
{
    public class XmlFileLicenseManager :  ILicenseManager
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
            get;
            private set;
        }

        public bool IsRegistered
        {
            get;
            protected set;
        }

        public ILicense[] GetLicenses()
        {
            List<ILicense> licenses = new List<ILicense>();
            foreach (string licFile in Directory.GetFiles(AppContext.Current["Core"].CodeBase, "*.lic"))
            {
                XmlFileLicense lic = new XmlFileLicense(licFile);
                if (lic.IsValid)
                    licenses.Add(lic);
            }
            return licenses.ToArray();
        }

        public void Initialize(string name, params string[] args)
        {
            IsInitialized = true;
        }

        public void Register(IContainerMappings mappings)
        {
            mappings.Add().Map<ILicenseManager>(this);
            this.IsRegistered = true;
        }
    }
}

using Common.DI;
using Common.Licensing;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Common.Application
{
    public delegate void ApplicationContextLoadedHandler(bool success);
    public interface IApplicationContext
    {
        event ApplicationContextLoadedHandler Loaded;

        int Run(AppContext applicationDomain, params string[] args);
        IContainer CreateContainer();
        IPluginLoader CreateLoader();
        ILicenseManager CreateLicenseManager();
        ILicensedInstaller CreateLicensedAppInstaller();
        void ApplyLicenses(ILicense[] license);
        void Quit(int exitCode);
        bool IsLoaded { get; }

        void Raise<T>(Subscription subscription, T args) where T : EventArgs;
    }
}

using Common.DI;
using Common.Diagnostics;
using Common.Licensing;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Common.Application
{
    

    public abstract class ApplicationContextBase : IApplicationContext
    {
        public event ApplicationContextLoadedHandler Loaded;

        public int Run(AppContext applicationDomain, params string[] args)
        {
            this.Domain = applicationDomain;
            this.Args = args;
            this.OnLoad();
            if (applicationDomain.Setup.IsHosted)
            {
                return RunHosted();
            }
            else
            {
                return RunUnhosted();
            }
        }

        public virtual void ApplyLicenses(ILicense[] licenses) { }

        
        public abstract IContainer CreateContainer();
        public virtual IPluginLoader CreateLoader()
        {
            var ctxAssembly = OnGetApplicationContextAssembly();
            var loaderType = ctxAssembly.GetCustomAttribute<PluginLoaderAttribute>();
            if (loaderType == null)
            {
                return new NoPluginLoader();
            }
            else
            {
                return (IPluginLoader)Activator.CreateInstance(loaderType.LoaderType);
            }
        }

        public virtual ILicenseManager CreateLicenseManager()
        {
            var ctxAssembly = OnGetApplicationContextAssembly();
            var loaderType = ctxAssembly.GetCustomAttribute<LicenseManagerAttribute>();
            if (loaderType == null)
            {
                return new NoLicenseManager();
            }
            else
            {
                return (ILicenseManager)Activator.CreateInstance(loaderType.LicenseManagerType);
            }
        }

        public virtual ILicensedInstaller CreateLicensedAppInstaller()
        {
            var ctxAssembly = OnGetApplicationContextAssembly();
            var loaderType = ctxAssembly.GetCustomAttribute<LicensedAppInstallerAttribute>();
            if (loaderType == null)
            {
                return new NoLicensedAppInstaller();
            }
            else
            {
                return (ILicensedInstaller)Activator.CreateInstance(loaderType.LicensedAppInstallerType);
            }
        }

        protected abstract int RunHosted();
        protected abstract int RunUnhosted();

        protected abstract bool IsAsyncLoaded { get; }

        public AppContext Domain { get; protected set; }
        public string[] Args { get; protected set; }
        public bool IsLoaded { get; private set; }

        protected virtual Assembly OnGetApplicationContextAssembly()
        {
            string ctxAssemblyPath = ConfigurationManager.AppSettings["ContextAssembly"];
            var ctxAssembly = Assembly.Load(ctxAssemblyPath);
            return ctxAssembly;
        }

        protected virtual void OnLoad()
        {
            if (IsAsyncLoaded)
            {
                var loader = new Thread(new ThreadStart(OnBackgroundLoad));
                loader.IsBackground = true;
                loader.Name = "Service Plugin Background Loader";
                loader.Start();
            }
            else
            {
                this.Domain.Loader.LoadComplete += OnLoadComplete;
                this.Domain.Loader.LoadPlugins();
            }
        }

        protected virtual void OnBackgroundLoad()
        {
            Common.AppContext.Current = this.Domain;
            Logger.Log("Loading Background Plugins");
            this.Domain.Loader.LoadComplete += OnLoadComplete;
            this.Domain.Loader.LoadPlugins();
        }

        public void Quit(int exitCode)
        {
            //save user prefs
            //gather blade paths

            OnQuit(exitCode);
        }

        protected void OnQuit(int exitCode)
        {
            OnExit(false);
            this.Domain.ExitCode = exitCode;
            this.Domain.Setup.RequestUnload(this.Domain);
        }

        protected virtual void OnLoadComplete()
        {
            Console.WriteLine("ApplicationContextBase.LoadComplete");
            OnLoaded(true);
        }

        protected virtual void OnLoaded(bool success)
        {
            Loaded?.Invoke(success);
            this.IsLoaded = success;
        }

        protected virtual bool OnExit(bool forced)
        {
            return true;
        }

        public virtual void Raise<T>(Subscription subscription, T args) where T : EventArgs
        {
            subscription.Raise(args);
        }
    }
}

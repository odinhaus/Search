using Common.Application;
using Common.Diagnostics;
using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Configuration.Install;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Common.Licensing
{
    public abstract class LicensedInstallerPlugin : LicensedPlugin, ILicensedInstaller
    {
        public bool IsInstalled
        {
            get;
            protected set;
        }

        public IEnumerable<DeclaredApp> Apps { get { return AppContext.Current.Apps; } }

        public void Install(params string[] appNames)
        {
            if (IsInstalled) return;
            if (!IsInitialized) Initialize(this.Name, appNames);

            OnInstallBegin();
            bool isInstalled = true;
            DeclaredApp current = AppContext.Current.CurrentApp;
            if (appNames != null && appNames.Length > 0)
            {
                foreach (string appName in appNames)
                {
                    DeclaredApp app = AppContext.Current[appName];
                    if (app != null)
                    {
                        AppContext.Current.CurrentApp = app;
                        OnCreateAppNodeIdentity(app);
                        isInstalled &= OnInstall(app);
                    }
                }
            }
            else
            {
                foreach (DeclaredApp app in AppContext.Current.Apps)
                {
                    if (app != null)
                    {
                        AppContext.Current.CurrentApp = app;
                        OnCreateAppNodeIdentity(app);
                        isInstalled &= OnInstall(app);
                    }
                }
            }
            AppContext.Current.CurrentApp = current;
            IsInstalled = isInstalled;
            OnInstallEnd();
        }

        protected virtual void OnInstallBegin() { }
        protected virtual void OnInstallEnd() { }
        protected abstract void OnCreateAppNodeIdentity(DeclaredApp app);

        /// <summary>
        /// Derived types should override to implement installation behavior.  This method will be called 
        /// for each target app context that the installer is configured to run for using its InstallerComponentAttribute.
        /// </summary>
        /// <returns>true when install was successful</returns>
        protected abstract bool OnInstall(DeclaredApp app);

        protected bool RunInstallers(params Assembly[] assemblies)
        {
            bool success = true;
            foreach (Assembly assembly in assemblies)
            {
                Type[] installers = assembly.GetTypes().Where(t => typeof(Installer).IsAssignableFrom(t)).ToArray();
                foreach (Type installer in installers)
                {
                    RunInstallerAttribute attrib = installer.GetCustomAttribute<RunInstallerAttribute>(true);
                    if (attrib != null
                        && attrib.RunInstaller)
                    {
                        Installer i = (Installer)Activator.CreateInstance(installer, new object[] { });
                        IDictionary stateSaver = new Hashtable();
                        try
                        {
                            i.Install(stateSaver);
                            i.Commit(stateSaver);
                        }
                        catch (Exception ex)
                        {
                            Logger.LogError(ex, "Failed to install " + i.GetType().Name + " in assembly " + assembly.FullName);
                            i.Rollback(stateSaver);
                            success = false;
                        }
                    }
                }
            }
            return success;
        }
    }
}

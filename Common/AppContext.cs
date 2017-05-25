using Common.DI;
using Common.Diagnostics;
using Common.Security;
using Common.Web;
using StructureMap;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Principal;
using System.Text;
using System.Threading.Tasks;

namespace Common
{

    public enum Environment
    {
        Production,
        Test,
        Beta,
        Demo,
        Development,
        Custom
    }


    public partial class AppContext : MarshalByRefObject
    {
        static AppContext()
        {
            AppContexts = new Dictionary<string, AppContextSetup>();
            foreach (var setting in ConfigurationManager.AppSettings.AllKeys)
            {
                _env.Add(setting, ConfigurationManager.AppSettings[setting]);
            }
        }

        private static string[] Args { get; set; }
        private static Dictionary<string, AppContextSetup> AppContexts { get; set; }
        public static Environment CurrentEnvironment { get; set; }
        public static IResolveApiUris ApiUris { get { return Current.Container.GetInstance<IResolveApiUris>(); } }
        public static IPrincipal CurrentPrincipal {  get { return SecurityContext.Current.CurrentPrincipal; } }
        public static string Name { get { return ConfigurationManager.AppSettings["ProductName"]; } }
        public static bool Hosted { get; private set; }

        

        //========================================================================================================//
        /// <summary>
        /// Call this method from the Main STAThread, or Application_Start.Global.asax to run the shell application
        /// </summary>
        /// <param name="args">the startup arguments to use when running the app context, typically passed in as command line arguments</param>
        /// <param name="createAppContext">pass true if the AppContext should host the runtime in a new app domain, and enable app updates, otherwise pass false</param>
        /// <returns></returns>
        public static int Run(string[] args = null, bool createAppContext = true)
        {
            reload:
            Hosted = createAppContext;
            Args = args == null ? new string[0] : args;
            if (createAppContext)
            {
                var domain = CreateAppDomain(args);
                domain.Instance.RunChild(createAppContext);
                domain.ExitWaitHandle.WaitOne();

                if (domain.IsReloading)
                {
                    string name = domain.AppDomain.FriendlyName;

                    if (domain.RunAsService)
                    {
                        System.Environment.Exit(2); // services can't be reloaded - need to trigger a restart at the OS level
                    }
                    else
                    {
                        if (AppContexts.ContainsKey(domain.Key))
                            AppContexts.Remove(domain.Key);

                        Logger.LogInfo("Unloading AppDomain " + name + ".");
                        try
                        {
                            AppDomain.Unload(domain.AppDomain);
                            Logger.LogInfo("Unloaded AppDomain " + name + ".");
                        }
                        catch (Exception ex)
                        {
                            Logger.LogError(ex, "AppDomain unload failed for " + name + ".");
                        }

                        Logger.LogInfo("Attempting to restart AppDomain " + name + "...");
                        goto reload;
                    }
                }
                return domain.ReturnCode;
            }
            else
            {
                var domain = CreateLocalDomain(args);
                return domain.Instance.RunChild(createAppContext);
            }
        }

        private static AppContextSetup CreateLocalDomain(string[] args)
        {
            var appAssembly = Assembly.GetEntryAssembly();
            if (appAssembly == null)
            {
                appAssembly = Assembly.GetExecutingAssembly();
            }

            string appName = appAssembly.FullName;

            AppDomain domain = AppDomain.CurrentDomain;

            var domainInstance = new AppContextSetup(appAssembly, args, false, true);

            var child = new AppContext();
            child.Setup = domainInstance;
            domainInstance.Instance = child;
            AppContexts.Add(domainInstance.Key, domainInstance);
            return domainInstance;
        }

        //========================================================================================================//

        //========================================================================================================//
        /// <summary>
        /// Creates the child app domain
        /// </summary>
        /// <param name="context"></param>
        /// <param name="args"></param>
        /// <returns></returns>
        private static AppContextSetup CreateAppDomain(string[] args)
        {
            AppSettingsSection section = (AppSettingsSection)ConfigurationManager.GetSection("AppSettings"); // primes the config manger CurrentConfig value

            Assembly appAssembly = Assembly.GetEntryAssembly();
            if (appAssembly == null)
            {
                appAssembly = Assembly.GetExecutingAssembly();
            }

            string appName = appAssembly.FullName;
            var codebase = new AppContext().CodeBase; // gets the path to the host executable directory

            string configPath = "";
            try
            {
                configPath = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None).FilePath;
            }
            catch
            {
                configPath = System.Web.Configuration.WebConfigurationManager.OpenWebConfiguration(@"\").FilePath;
            }

            AppDomainSetup ads = new AppDomainSetup()
            {
                DisallowBindingRedirects = false,
                DisallowCodeDownload = false,
                ShadowCopyFiles = "true",
                ApplicationName = appAssembly.GetName().Name,
                CachePath = Path.Combine(codebase, "Shadow"),
                ApplicationBase = codebase,
                PrivateBinPath = Path.Combine(codebase, "Apps"),
                ShadowCopyDirectories = codebase + ";" + Path.Combine(codebase, "Apps"),
                ConfigurationFile = configPath,
            };

            DeleteDirectory(Path.Combine(ads.CachePath, ads.ApplicationName));

            Logger.LogInfo("Creating AppDomain " + appName + "...");
            AppDomain domain = AppDomain.CreateDomain(appName, null, ads);
            var api = (AppContextSetup)domain.CreateInstanceAndUnwrap(
                typeof(AppContextSetup).Assembly.FullName,
                typeof(AppContextSetup).FullName,
                false,
                BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance,
                null,
                new object[] { appAssembly, args, true, true },
                null,
                null);

            var instance = api.CreateAppInstance();
            api.Instance = instance;
            instance.Setup = api;
            AppContexts.Add(api.Key, api);
            return api;
        }

        public static void DeleteDirectory(string target_dir)
        {
            if (!Directory.Exists(target_dir)) return;
            string[] files = Directory.GetFiles(target_dir);
            string[] dirs = Directory.GetDirectories(target_dir);
            try
            {
                foreach (string file in files)
                {
                    System.IO.File.SetAttributes(file, FileAttributes.Normal);
                    System.IO.File.Delete(file);
                }

                foreach (string dir in dirs)
                {
                    DeleteDirectory(dir);
                }
                try
                {
                    Directory.Delete(target_dir, false);
                }
                catch { }
            }
            catch { }
        }
    }
}

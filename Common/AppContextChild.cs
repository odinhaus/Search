using Common.Application;
using Common.Configuration;
using Common.DI;
using Common.Diagnostics;
using Common.Licensing;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace Common
{
    public delegate void EnvironmentVariableChangedHandler(object sender, PropertyChangedEventArgs e);

    public partial class AppContext : IAppSource
    {
        public static event EnvironmentVariableChangedHandler EnvironmentVariableChanged;

        static Dictionary<string, object> _env = new Dictionary<string, object>();
        private DeclaredApp _core;

        protected AppContext()
        {
            AppDomain.CurrentDomain.FirstChanceException += ChildDomain_FirstChanceException;
            AppDomain.CurrentDomain.UnhandledException += ChildDomain_UnhandledException;
            AppDomain.CurrentDomain.AssemblyResolve += ChildDomain_AssemblyResolve;
            AppDomain.CurrentDomain.AssemblyLoad += ChildDomain_AssemblyLoad;
            AppDomain.CurrentDomain.DomainUnload += ChildDomain_DomainUnload;
            AppDomain.CurrentDomain.ReflectionOnlyAssemblyResolve += ChildDomain_ReflectionOnlyAssemblyResolve;
            AppDomain.CurrentDomain.ResourceResolve += ChildDomain_ResourceResolve;
            try
            {
                AppDomain.CurrentDomain.SetData("CodeBase", ConfigurationManager.AppSettings["CodeBase"]);
            }
            catch { }
        }



        //========================================================================================================//
        /// Gets the Dependency Injection container used to resolve plugin instances
        /// </summary>
        public Common.DI.IContainer Container { get; private set; }
        //========================================================================================================//



        //========================================================================================================//
        /// Gets the plugin loader for the AppContext
        /// </summary>
        public IPluginLoader Loader { get; private set; }
        //========================================================================================================//


        //========================================================================================================//
        /// <summary>
        /// AppContext was successfully loaded
        /// </summary>
        public bool IsLoaded { get { return ApplicationContext.IsLoaded; } }
        //========================================================================================================//


        //========================================================================================================//
        /// <summary>
        /// Raises a global event that can received by all subscribers that register a callback via the Subscribe 
        /// method whose generic type matches the type T for this method.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        public void Raise<T>(T args) where T : EventArgs
        {
            lock(_subscriptions)
            {
                List<Subscription> actions;
                if (_subscriptions.TryGetValue(typeof(T), out actions))
                {
                    foreach(var subscription in actions)
                    {
                        if (!(args is CancelEventArgs) || !((CancelEventArgs)(object)args).Cancel)
                        {
                            this.ApplicationContext.Raise(subscription, args);
                        }
                    }
                }
            }
        }
        //========================================================================================================//


        Dictionary<Type, List<Subscription>> _subscriptions = new Dictionary<Type, List<Subscription>>();
        //========================================================================================================//
        /// <summary>
        /// Raises a global event that can received by all subscribers that register a callback via the Subscribe 
        /// method whose generic type matches the type T for this method.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <returns>disposable subscription reference, governing the lifetime of the callback.  disposing the 
        /// subscription, prevents the callback from being called.</returns>
        public Subscription Subscribe<T>(Action<T> callback) where T : EventArgs
        {
            lock (_subscriptions)
            {
                List<Subscription> actions;
                if (!_subscriptions.TryGetValue(typeof(T), out actions))
                {
                    actions = new List<Subscription>();
                    _subscriptions.Add(typeof(T), actions);
                }
                var subscription = new Subscription<T>(callback, (s) => actions.Remove(s));
                actions.Add(subscription);
                return subscription;
            }
        }
        //========================================================================================================//


        //========================================================================================================//
        /// Gets the LicenseManager for the AppContext
        /// </summary>
        public ILicenseManager LicenseManager { get; private set; }
        //========================================================================================================//



        //========================================================================================================//
        /// Gets the LicensedAppInstaller for the AppContext
        /// </summary>
        public ILicensedInstaller LicensedAppInstaller { get; private set; }
        //========================================================================================================//


        //========================================================================================================//
        /// <summary>
        /// Gets the ApplicationContext for the current domain
        /// </summary>
        protected IApplicationContext ApplicationContext { get; private set; }
        //========================================================================================================//


        //========================================================================================================//
        /// Gets/sets the exit code for the AppContext
        /// </summary>
        public int ExitCode { get; set; }
        //========================================================================================================//



        //========================================================================================================//
        /// Gets the current AppContext instance
        /// </summary>
        public static AppContext Current { get; internal set; }
        //========================================================================================================//


        //========================================================================================================//
        /// <summary>
        /// Gets the AppContext setup information used when the AppContext was created
        /// </summary>
        public AppContextSetup Setup { get; private set; }
        //========================================================================================================//


        //========================================================================================================//
        /// <summary>
        /// Gets all the Apps currently discovered and loaded in the AppContext
        /// </summary>
        public IEnumerable<DeclaredApp> Apps
        {
            get
            {
                if (Container == null)
                {
                    return new DeclaredApp[0];
                }
                else
                {
                    IAppSource appSource = Container.GetInstance<IAppSource>();
                    List<DeclaredApp> apps = new List<DeclaredApp>();
                    apps.Add(_core);
                    if (appSource != null)
                    {
                        apps.AddRange(appSource.Apps);
                    }
                    return apps.ToArray();
                }
            }
        }
        //========================================================================================================//



        //========================================================================================================//
        /// <summary>
        /// Gets a discovered App by its app name, if it exists, otherwise this returns null
        /// </summary>
        /// <param name="appName"></param>
        /// <returns></returns>
        public DeclaredApp this[string appName]
        {
            get
            {
                return Apps.Where(a => a.Name.Equals(appName, StringComparison.InvariantCultureIgnoreCase)).FirstOrDefault();
            }
        }
        //========================================================================================================//


        [ThreadStatic]
        private DeclaredApp _currentApp = null;
        //========================================================================================================//
        /// <summary>
        /// Gets/sets the current app for the current context.  If no explicit app has been set, the default Core 
        /// app will be set by default.  This value is thread-specific.
        /// </summary>
        public DeclaredApp CurrentApp
        {
            get
            {
                if (_currentApp == null && AppContext.Current != null)
                    return AppContext.Current["Core"];
                else return _currentApp;
            }
            set
            {
                _currentApp = value;
            }
        }
        //========================================================================================================//

        //========================================================================================================//
        /// <summary>
        /// Gets the location on disk of the main codebase for the AppContext
        /// </summary>
        public string CodeBase
        {
            get
            {
                if (CurrentApp == null || CurrentApp.CodeBase == null)
                {
                    string path = System.IO.Path.GetDirectoryName(Assembly.GetExecutingAssembly().CodeBase.Replace(@"file:///", ""));
                    string codeBase = AppDomain.CurrentDomain.GetData("CodeBase") as string;
                    if (string.IsNullOrEmpty(codeBase))
                    {
                        codeBase = path;
                    }
                    return codeBase;
                }
                else return CurrentApp.CodeBase;
            }
        }
        //========================================================================================================//


        //========================================================================================================//
        /// <summary>
        /// Gets a boolean indicating whether the AppContext is still in an activeky running state
        /// </summary>
        public bool IsRunning { get; private set; }
        //========================================================================================================//



        //========================================================================================================//
        /// <summary>
        /// Gets an environment variable (including all AppSettings) by name, and returns a default value if the setting is missing
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="key"></param>
        /// <param name="defaultValue"></param>
        /// <returns></returns>
        public static T GetEnvironmentVariable<T>(string key, T defaultValue)
        {
            lock(_env)
            {
                object value;
                if (_env.TryGetValue(key, out value))
                {
                    try
                    {
                        return (T)value;
                    }
                    catch(InvalidCastException)
                    {
                        var  converter = TypeDescriptor.GetConverter(typeof(T));
                        if (converter.CanConvertFrom(value.GetType()))
                        {
                            return (T)converter.ConvertFrom(value);
                        }
                    }
                }
                return defaultValue;
            }
        }
        //========================================================================================================//


        //========================================================================================================//
        /// <summary>
        /// Sets an environment variable to the value supplied
        /// </summary>
        /// <param name="key"></param>
        /// <param name="value"></param>
        public static void SetEnvironmentVariable(string key, object value)
        {
            if (_env.ContainsKey(key))
                _env[key] = value;
            else
                _env.Add(key, value);

            if (EnvironmentVariableChanged != null)
            {
                EnvironmentVariableChanged(typeof(AppContext), new PropertyChangedEventArgs(key));
            }
        }
        //========================================================================================================//


        //========================================================================================================//
        /// <summary>
        /// Copies startup args and config settings to context environment variables
        /// </summary>
        /// <param name="args"></param>
        public static void SetEnvironmentVariables(string[] args)
        {
            foreach (var key in ConfigurationManager.AppSettings.AllKeys)
            {
                SetEnvironmentVariable(key, ConfigurationManager.AppSettings[key]);
            }

            if (args != null && args.Length > 0)
            {
                Regex r = new Regex(@"(?<Arg>[\w]+):(?<Value>.*)");
                foreach (string arg in args)
                {
                    Match m = r.Match(arg);
                    if (m.Success)
                    {
                        string argName = m.Groups["Arg"].Value;
                        string argVal = m.Groups["Value"].Value;
                        SetEnvironmentVariable(argName, argVal);
                    }
                }
            }
        }
        //========================================================================================================//


        //========================================================================================================//
        /// <summary>
        /// Handles domain unloaded event
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ChildDomain_DomainUnload(object sender, EventArgs e)
        {
            OnDomainUnload(sender, e);
        }
        //========================================================================================================//

        //========================================================================================================//
        /// <summary>
        /// Derived types should overload to implement custom domain unload behavior
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        protected virtual void OnDomainUnload(object sender, EventArgs e)
        {

        }
        //========================================================================================================//

        //========================================================================================================//
        /// <summary>
        /// Handles assembly resolution for the domain
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="args"></param>
        /// <returns></returns>
        private Assembly ChildDomain_AssemblyResolve(object sender, ResolveEventArgs args)
        {
            return OnAssemblyResolve(sender, args);
        }
        //========================================================================================================//

        //========================================================================================================//
        /// <summary>
        /// Handles reflection only assembly resolution for the domain
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="args"></param>
        /// <returns></returns>
        private Assembly ChildDomain_ReflectionOnlyAssemblyResolve(object sender, ResolveEventArgs args)
        {
            return OnAssemblyResolveReflectionOnly(sender, args);
        }
        //========================================================================================================//

        //========================================================================================================//
        /// <summary>
        /// Handles resource resolution failures
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="args"></param>
        /// <returns></returns>
        private Assembly ChildDomain_ResourceResolve(object sender, ResolveEventArgs args)
        {
            return OnResourceResolve(sender, args);
        }
        //========================================================================================================//

        //========================================================================================================//
        /// <summary>
        /// Derived types should override to handle resource resolution failures
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="args"></param>
        /// <returns></returns>
        protected virtual Assembly OnResourceResolve(object sender, ResolveEventArgs args)
        {
            return OnAssemblyResolve(sender, args);
        }
        //========================================================================================================//

        protected Dictionary<string, Assembly> ResolvedAssemblies = new Dictionary<string, Assembly>();
        protected Dictionary<string, Assembly> ResolvedAssembliesReflectionOnly = new Dictionary<string, Assembly>();
        //========================================================================================================//
        /// <summary>
        /// Derived types should override to implement custom assembly resolution
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="args"></param>
        /// <returns></returns>
        protected virtual Assembly OnAssemblyResolve(object sender, ResolveEventArgs args)
        {
            AssemblyName an = new AssemblyName(args.Name);
            Assembly asm = null;
            if (!TryGetLoadedAssembly(an, false, out asm))
            {
                string[] split = args.Name.Split(',');
                string name = split[0];

                if (name.EndsWith(".resources"))
                {
                    return null;
                }
                bool retry = true;
                DeclaredApp app = AppContext.Current.CurrentApp;

                retrycore:
                try
                {
                    #region assembly name
                    if (split.Length == 4)
                        an = new AssemblyName(string.Format("{0}, {1}, {2}, {3}", name, split[1].Trim(), split[2].Trim(), split[3].Trim()));
                    else if (split.Length == 3)
                        an = new AssemblyName(string.Format("{0}, {1}, {2}", name, split[1].Trim(), split[2].Trim()));
                    else if (split.Length == 2)
                        an = new AssemblyName(string.Format("{0}, {1}, Culture={2}", name, split[1].Trim(), Thread.CurrentThread.CurrentCulture.DisplayName));
                    else if (split.Length == 1)
                        an = new AssemblyName(string.Format("{0}", name));
                    #endregion

                    if (TryGetLoadedAssembly(an, false, out asm)) return asm;

                    asm = Assembly.LoadFrom(Path.Combine(app.CodeBase, name) + ".dll");
                }
                catch { }

                if (asm == null)
                {
                    try
                    {
                        asm = Assembly.LoadFrom(Path.Combine(app.CodeBase, name) + ".exe");
                    }
                    catch { }
                }

                if (asm == null
                    && retry
                    && app != AppContext.Current["Core"])
                {
                    app = AppContext.Current["Core"];
                    retry = false;
                    goto retrycore;
                }

                if (asm == null
                    && !TryGetLoadedAssembly(an, true, out asm))
                {
                    Logger.LogWarn("Could not resolve assembly "
                        + args.Name
                        + " for "
                        + (args.RequestingAssembly == null ? "<null>" : args.RequestingAssembly.FullName)
                        + " in "
                        + AppDomain.CurrentDomain.FriendlyName + " AppDomain");
                }
                ResolvedAssemblies.Add(args.Name, asm);
            }
            return asm;
        }
        //========================================================================================================//


        protected bool TryGetLoadedAssembly(AssemblyName name, bool matchByNameOnly, out Assembly assembly)
        {
            bool found = false;
            assembly = null;
            if (matchByNameOnly)
            {
                List<Assembly> matches = new List<Assembly>();
                matches.AddRange(ResolvedAssemblies.Values
                    .Where(a => a != null && a.GetName().Name.Equals(name.Name, StringComparison.InvariantCultureIgnoreCase)));
                if (matches.Count == 0)
                {
                    matches.AddRange(AppDomain.CurrentDomain.GetAssemblies()
                      .Where(a => a.GetName().Name.Equals(name.Name, StringComparison.InvariantCultureIgnoreCase)));
                }

                if (matches.Count > 0)
                {
                    matches.Sort(delegate (Assembly asm1, Assembly asm2)
                    {
                        Version v1 = asm1.GetName().Version;
                        Version v2 = asm2.GetName().Version;
                        return -v1.CompareTo(v2);
                    });
                    assembly = matches.First();
                    found = true;
                }
            }
            else
            {
                if (ResolvedAssemblies.ContainsKey(name.FullName))
                {
                    assembly = ResolvedAssemblies[name.FullName];
                    found = true;
                }
                else if (AppDomain.CurrentDomain.GetAssemblies().Count(a => a.GetName().FullName.Equals(name.FullName, StringComparison.InvariantCultureIgnoreCase)) > 0)
                {
                    Assembly al = AppDomain.CurrentDomain.GetAssemblies()
                        .Where(a => a.GetName().FullName.Equals(name.FullName, StringComparison.InvariantCultureIgnoreCase)).First();
                    assembly = al;
                    found = true;
                }
            }
            return found;
        }

        //========================================================================================================//
        /// <summary>
        /// Derived types should override to implement custom reflection-only assembly resolution
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="args"></param>
        /// <returns></returns>
        protected virtual Assembly OnAssemblyResolveReflectionOnly(object sender, ResolveEventArgs args)
        {
            if (ResolvedAssembliesReflectionOnly.ContainsKey(args.Name))
            {
                return ResolvedAssembliesReflectionOnly[args.Name];
            }
            else
            {
                Assembly asm = null;
                try
                {
                    asm = Assembly.ReflectionOnlyLoad(args.Name);
                }
                catch
                {
                    string name = args.Name.Split(',')[0];
                    try
                    {
                        asm = Assembly.ReflectionOnlyLoadFrom(Path.ChangeExtension(Path.Combine(AppContext.Current.CodeBase, name), "dll"));
                    }
                    catch { }
                    if (asm == null)
                    {
                        try
                        {
                            asm = Assembly.ReflectionOnlyLoadFrom(Path.ChangeExtension(Path.Combine(AppContext.Current.CodeBase, name), "exe"));
                        }
                        catch { }
                    }
                }
                if (asm == null)
                {
                    Logger.LogWarn("Could not resolve assembly "
                        + args.Name
                        + " for "
                        + args.RequestingAssembly == null ? "<null>" : args.RequestingAssembly
                        + " in "
                        + AppDomain.CurrentDomain.FriendlyName + " AppDomain");
                }
                ResolvedAssembliesReflectionOnly.Add(args.Name, asm);
                return asm;
            }
        }
        //========================================================================================================//


        //========================================================================================================//
        /// <summary>
        /// Handles assembly load event for domain
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="args"></param>
        private void ChildDomain_AssemblyLoad(object sender, AssemblyLoadEventArgs args)
        {
            OnAssemblyLoad(sender, args);
        }
        //========================================================================================================//

        //========================================================================================================//
        /// <summary>
        /// Derived types should override to implement custom assembly load behavior
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="args"></param>
        protected virtual void OnAssemblyLoad(object sender, AssemblyLoadEventArgs args)
        {

        }
        //========================================================================================================//


        //========================================================================================================//
        /// <summary>
        /// Handles first chance exceptions for the App Domain
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ChildDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            OnUnhandledException(sender, e);
        }
        //========================================================================================================//

        //========================================================================================================//
        /// <summary>
        /// Derived implementation can provide their own logic to perform when an Unhandled Exception occurs
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        protected virtual void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            try
            {
                Logger.LogError(e.ExceptionObject.ToString());
            }
            catch
            {
                try
                {
                    //ObjectLogWriter.AppendObject(Path.Combine(Context.CurrentContext.CodeBase, "CrashLog.log"), e.ExceptionObject.ToString());
                }
                catch { }
            }
        }
        //========================================================================================================//

        //========================================================================================================//
        /// <summary>
        /// Handles unhandled exceptions for the App Domain
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ChildDomain_FirstChanceException(object sender, System.Runtime.ExceptionServices.FirstChanceExceptionEventArgs e)
        {
            OnFirstChanceException(sender, e);
        }
        //========================================================================================================//

        //========================================================================================================//
        /// <summary>
        /// Derived implementation can provide their own logic to perform when a First Chance Exception occurs
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        protected virtual void OnFirstChanceException(object sender, System.Runtime.ExceptionServices.FirstChanceExceptionEventArgs e)
        {
        }
        //========================================================================================================//


        //========================================================================================================//
        /// <summary>
        /// Called by app domain host to load the injection container
        /// </summary>
        /// <param name="args"></param>
        private int RunChild(bool createContext = true)
        {
            IsRunning = true;
            Hosted = createContext;
            AppContext.Current = this;

            SetEnvironmentVariables(Setup.Args);

            _core = new DeclaredApp()
            {
                Name = "Core",
                Version = typeof(AppContext).Assembly.GetName().Version.Major.ToString() + "." + typeof(AppContext).Assembly.GetName().Version.Minor.ToString(),
                CodeBase = CodeBase,
                IsLocal = true,
                Product = ConfigurationManager.AppSettings["ProductName"],
                Manifest = new DiscoveryManifest(),
                DeletePrevious = false,
                SourceUri = CodeBase,
                IncludeSourceBytes = false,
                IsCore = true
            };

            DiscoveryTarget target = new DiscoveryTarget()
            {
                Product = _core.Product
            };

            DiscoveryFileElement file = new DiscoveryFileElement()
            {
                LoadedAssembly = typeof(AppContext).Assembly,
                Reflect = true,
                IsPrimary = true,
                IsValid = true,
                Name = typeof(AppContext).Assembly.GetName().Name,
                CodeBase = _core.CodeBase
            };
            target.Files.Add(file);

            var pluginConfig = ConfigurationManager.GetSection("pluginLoader") as PluginLoaderConfiguration;

            foreach(var asm in pluginConfig.CoreAssemblies)
            {
                var path = Path.Combine(((CoreAssemblyElement)asm).Path, ((CoreAssemblyElement)asm).Assembly);
                var codeBase = ((CoreAssemblyElement)asm).Path;
                if (!Path.IsPathRooted(path))
                {
                    path = Path.Combine(_core.CodeBase, path);
                    codeBase = Path.Combine(_core.CodeBase, codeBase);
                }

                string asmPath = path + ".dll";

                if (!System.IO.File.Exists(asmPath))
                {
                    asmPath = path + ".exe";
                    if (!System.IO.File.Exists(asmPath))
                    {
                        Logger.LogWarn(new FileNotFoundException("The core assembly " + path + " could not be found."));
                        continue;
                    }
                }

                var coreFile = new DiscoveryFileElement()
                {
                    LoadedAssembly = Assembly.LoadFrom(asmPath),
                    Reflect = true,
                    IsValid = true,
                    Name = ((CoreAssemblyElement)asm).Assembly,
                    CodeBase = codeBase
                };
                target.Files.Add(coreFile);
            }

            _core.Manifest.Targets.Add(target);

            CurrentApp = _core;

            ApplicationContext = OnCreateContext();
            ApplicationContext.Loaded += ApplicationContext_Loaded;
            Container = ApplicationContext.CreateContainer();
            Loader = ApplicationContext.CreateLoader();
            Container.Map(Loader);
            Loader.SetContainer(Container);
            LicenseManager = ApplicationContext.CreateLicenseManager();
            Container.Map(LicenseManager);
            LicensedAppInstaller = ApplicationContext.CreateLicensedAppInstaller();
            Container.Map(LicensedAppInstaller);
            ApplicationContext.ApplyLicenses(LicenseManager.GetLicenses());

            IsRunning = false;
            return ApplicationContext.Run(this, Setup.Args);
        }

        public event ApplicationContextLoadedHandler Loaded;
        private void ApplicationContext_Loaded(bool success)
        {
            Loaded?.Invoke(success);
        }

        //========================================================================================================//



        //========================================================================================================//
        /// <summary>
        /// Quits the current application context
        /// </summary>
        /// <param name="exitCode"></param>
        public void Quit(int exitCode)
        {
            ApplicationContext.Quit(exitCode);
        }
        //========================================================================================================//

        


        //========================================================================================================//
        /// <summary>
        /// Creates the ApplicationContext instance by resolving the ApplicationCOntextAttribute in the assembly designated by the AppSettings["ContextAssembly"] value
        /// </summary>
        /// <returns></returns>
        protected virtual IApplicationContext OnCreateContext()
        {
            string shellAssemblyPath = ConfigurationManager.AppSettings["ContextAssembly"];
            Assembly shellAssembly = Assembly.Load(shellAssemblyPath);

            var contextType = shellAssembly.GetCustomAttribute<ApplicationContextAttribute>();

            // get the shell atributes, there should only be one
            if (contextType == null)
            {
                throw (new InvalidOperationException("An ApplicationContextAttribute could not be found."));
            }

            var context = (IApplicationContext)Activator.CreateInstance(contextType.ContextType, null);

            if (!typeof(AppContext).Assembly.Equals(contextType.GetType().Assembly))
            {
                CurrentApp.Manifest.Targets[0].Files.Add(new DiscoveryFileElement()
                {
                    LoadedAssembly = contextType.GetType().Assembly,
                    Reflect = true,
                    IsPrimary = false,
                    IsValid = true,
                    Name = contextType.GetType().Assembly.GetName().Name,
                    CodeBase = _core.CodeBase
                });
            }

            return context;
        }
        //========================================================================================================//

    }
}

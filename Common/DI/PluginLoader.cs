using Common.Application;
using Common.Configuration;
using Common.Diagnostics;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Common.DI
{
    /// <summary>
    /// Simple class that uses reflection to detect ComponentAttribute declarations
    /// at the assembly level for all .exe and .dll files in the execution
    /// directory that define components to inject into the shell
    /// </summary>
    public class PluginLoader : IPluginLoader
    {
        //List<object> _components = new List<object>();
        List<PluginAttribute> _attribs = new List<PluginAttribute>();

        #region IpluginLoader Members

        public event PluginLoadStatusHandler LoadStatus;

        public event PluginLoadCompleteHandler LoadComplete;

        public event PluginLoadBeginHandler LoadBegin;

        public event PluginLoadCoreBegin LoadCoreBegin;
        public event PluginLoadCoreComplete LoadCoreComplete;
        public event PluginLoadExtensionsBegin LoadExtensionsBegin;
        public event PluginLoadExtensionsComplete LoadExtensionsComplete;


        public void LoadPlugins(params string[] args)
        {
            string _root = AppDomain.CurrentDomain.ShadowCopyFiles
                ? Path.Combine(AppDomain.CurrentDomain.SetupInformation.CachePath, AppDomain.CurrentDomain.SetupInformation.ApplicationName)
                : AppDomain.CurrentDomain.BaseDirectory;

            LoaderConfig = ConfigurationManager.GetSection("pluginLoader") as PluginLoaderConfiguration;
            List<PluginAttribute> loaded = new List<PluginAttribute>();

            if (this.LoadBegin != null)
                this.LoadBegin();

            // load core components first
            LoadCoreAssemblies(LoaderConfig.CoreAssemblies);
            LoadCorePluginsLoop(LoaderConfig, loaded);

            LoadDiscoverableAssemblies(LoaderConfig.DiscoveryPaths);
            LoadExtensionsPluginsLoop(LoaderConfig, loaded);


            IsComplete = true;
            if (this.LoadComplete != null)
            {
                this.LoadComplete();
            }
        }

        public PluginLoaderConfiguration LoaderConfig { get; private set; }
        public IContainer Container { get; private set; }

        public void SetContainer(IContainer container)
        {
            this.Container = container;
        }

        private void LoadCorePluginsLoop(PluginLoaderConfiguration section, List<PluginAttribute> loaded)
        {
            bool isFirstPass = true;
            IEnumerable<PluginAttribute> plugins = null;

            do
            {
                //IEnumerable<ComponentAttribute> dbComponents = DataContext.Default.Select<ComponentAttribute>();
                plugins = GetPluginList(
                    section, 
                    loaded, 
                    //dbComponents, 
                    false);

                Dictionary<string, PluginDependency> cd = PluginDependency.BuildDependencyGraph(plugins.ToArray());

                _count += plugins.Count();

                if (isFirstPass)
                {
                    if (this.LoadCoreBegin != null)
                        this.LoadCoreBegin();
                    isFirstPass = false;
                }

                LoadPluginsRecurse(cd);
                loaded.AddRange(plugins);

            } while (plugins.Count() > 0);
            if (this.LoadCoreComplete != null)
                this.LoadCoreComplete();
        }

        private void LoadExtensionsPluginsLoop(PluginLoaderConfiguration section, List<PluginAttribute> loaded)
        {
            bool isFirstPass = true;
            IEnumerable<PluginAttribute> plugins = null;
            DeclaredApp current = AppContext.Current.CurrentApp;
            foreach (DeclaredApp app in AppContext.Current.Apps)
            {
                if (app.IsCore) continue;
                AppContext.Current.CurrentApp = app;
                do
                {
                    //IEnumerable<ComponentAttribute> dbComponents = DataContext.Default.Select<ComponentAttribute>(new { NodeId = NodeIdentity.NodeId });
                    plugins = GetPluginList(
                        section, 
                        loaded, 
                        //dbComponents, 
                        true);

                    Dictionary<string, PluginDependency> cd = PluginDependency.BuildDependencyGraph(plugins.ToArray());

                    _count += plugins.Count();

                    if (isFirstPass)
                    {
                        if (this.LoadExtensionsBegin != null)
                            this.LoadExtensionsBegin();
                        isFirstPass = false;
                    }

                    LoadPluginsRecurse(cd);
                    loaded.AddRange(plugins);
                } while (plugins.Count() > 0);
            }
            AppContext.Current.CurrentApp = current;
            if (this.LoadExtensionsComplete != null)
                this.LoadExtensionsComplete();
        }

        private IEnumerable<PluginAttribute> GetPluginList(PluginLoaderConfiguration section,
            IEnumerable<PluginAttribute> exclude,
            //IEnumerable<ComponentAttribute> dbComponents,
            bool allowExtensions)
        {
            List<PluginAttribute> plugins = new List<PluginAttribute>();
            if (!allowExtensions)
            {
                foreach (CoreAssemblyElement cae in section.CoreAssemblies)
                {
                    if (cae.IsValid)
                    {
                        plugins.AddRange(ReflectAssembly(cae.LoadedAssembly));
                    }
                }

            }
            else
            {
                foreach (DeclaredApp app in AppContext.Current.LicensedAppInstaller.Apps)
                {
                    foreach (DiscoveryTarget target in app.Manifest.Targets.Where(t => t.IsLocal))
                    {
                        foreach (DiscoveryFileElement file in target.Files.Where(f => f.Reflect && f.IsValid && f.Exists))
                        {
                            plugins.AddRange(ReflectAssembly(file.LoadedAssembly));
                        }
                    }
                }
            }

            if (exclude != null)
            {
                foreach (PluginAttribute ca in exclude)
                {
                    PluginAttribute cap = plugins.Where(
                        c => c.Name.Equals(ca.Name, StringComparison.InvariantCultureIgnoreCase)).FirstOrDefault();
                    plugins.Remove(cap);
                }
            }
            return plugins;
        }

        private bool IsCore(PluginAttribute attribute)
        {
            foreach (CoreAssemblyElement coreAsm in LoaderConfig.CoreAssemblies)
            {
                if (coreAsm.Assembly.Trim().Equals(AssemblyName(attribute), StringComparison.InvariantCultureIgnoreCase))
                    return true;
            }
            return false;
        }

        private string AssemblyName(PluginAttribute attribute)
        {
            string[] split = attribute.Plugin.Split(',');
            return split[1].Trim();
        }

        private IEnumerable<PluginAttribute> ReflectAssembly(Assembly asm)
        {
            List<PluginAttribute> plugins = new List<PluginAttribute>();
            object[] attribs = asm.GetCustomAttributes(typeof(PluginAttribute), true);

            if (attribs != null && attribs.Length > 0)
            {
                plugins.AddRange(((PluginAttribute[])attribs).Where(a => a.Reflect));
            }

            return plugins;
        }

        private int _count;
        private int _index = 0;
        private void LoadPluginsRecurse(Dictionary<string, PluginDependency> cd)
        {
            // load all the siblings first
            Dictionary<string, PluginDependency>.Enumerator cdEn = cd.GetEnumerator();
            while (cdEn.MoveNext())
            {
                PluginDependency pluginsDependency = cdEn.Current.Value;

                if (!pluginsDependency.Created
                    && pluginsDependency.PluginAttribute.Enabled
                    && _attribs.Count(ca => ca.Name.Equals(pluginsDependency.PluginAttribute.Name, StringComparison.InvariantCultureIgnoreCase)
                    && ca.Plugin.Equals(pluginsDependency.PluginAttribute.Plugin, StringComparison.InvariantCultureIgnoreCase)) == 0)
                {
                    object plugin = null;
                    if (pluginsDependency.PluginAttribute.IsValid)
                    {
                        plugin = Activator.CreateInstance(pluginsDependency.PluginAttribute.PluginType,
                            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance,
                            null,
                            pluginsDependency.PluginAttribute.CtorArgs,
                            Thread.CurrentThread.CurrentCulture);
                    }
                    else
                    {
                        plugin = TypeHelper.CreateType(pluginsDependency.PluginAttribute.Plugin, new object[] { });
                    }
                    pluginsDependency.PluginAttribute.Instance = plugin;
                    pluginsDependency.Created = true;
                    if (plugin != null)
                    {
                        if (pluginsDependency.PluginAttribute.TargetType != null && pluginsDependency.PluginAttribute.TargetType != pluginsDependency.PluginAttribute.PluginType)
                        {
                            Container.Map(plugin, pluginsDependency.PluginAttribute.TargetType);
                        }
                        else
                        {
                            Container.Map(plugin, pluginsDependency.PluginAttribute.Name);
                        }
                        _attribs.Add(pluginsDependency.PluginAttribute);
                        NotifyPluginLoaded(plugin, pluginsDependency.PluginAttribute.Name, _index++, _count);
                    }
                }
            }

            // now load children
            foreach (PluginDependency dep in cd.Values)
            {
                LoadPluginsRecurse(dep.Dependencies);
            }
        }

        private void NotifyPluginLoaded(object plugin, string name, int index, int count)
        {
            if (plugin != null && LoadStatus != null)
            {
                LoadStatus(
                    new PluginLoadStatusEventArgs("Discovered plugin " + name + ".",
                    plugin,
                    name,
                    index,
                    count));
            }
        }

        //Dictionary<string, object> _injectedComponents = new Dictionary<string, object>();

        public bool IsComplete
        {
            get;
            private set;
        }

        public void Cancel()
        {
        }

        #endregion
        private void LoadCoreAssemblies(CoreAssemblyCollection coreAssemblyCollection)
        {
            List<Assembly> currentAssemblies = new List<Assembly>(AppDomain.CurrentDomain.GetAssemblies());
            foreach (CoreAssemblyElement cassembly in coreAssemblyCollection)
            {
                string path = cassembly.CodeBase + ".dll";
                bool exists = true;
                if (!System.IO.File.Exists(path))
                {
                    path = cassembly.CodeBase + ".exe";
                    if (!System.IO.File.Exists(path))
                    {
                        exists = false;
                        Logger.LogWarn(new FileNotFoundException("The core assembly " + cassembly.CodeBase + " could not be found."));
                    }
                }

                if (exists)
                {
                    AssemblyName name;
                    CoreAssemblyAttribute attrib;
                    if (!currentAssemblies.Contains<CoreAssemblyAttribute>(path, out name, out attrib))
                    {
                        if (attrib != null)
                            currentAssemblies.Add(AppDomain.CurrentDomain.Load(name));
                    }
                    cassembly.IsValid = attrib != null;
                    cassembly.LoadedAssembly = currentAssemblies
                        .Where(ca => ca.GetName().FullName.Equals(name.FullName, StringComparison.InvariantCultureIgnoreCase))
                        .FirstOrDefault();
                }
            }
        }

        private void LoadDiscoverableAssemblies(DiscoveryPathCollection discoveryPathCollection)
        {
            if (AppContext.Current.LicensedAppInstaller == null) return;

            List<Assembly> currentAssemblies = new List<Assembly>(AppDomain.CurrentDomain.GetAssemblies());

            foreach (DeclaredApp app in AppContext.Current.LicensedAppInstaller.Apps.Where(a => a.Manifest != null))
            {
                DiscoveryTarget target = app.Manifest.Targets.Where(t => t.IsLocal).FirstOrDefault();
                if (target != null)
                {
                    foreach (DiscoveryFileElement file in target.Files.Where(f => f.Reflect && f.Exists))
                    {
                        AssemblyName name;
                        PluginAssemblyAttribute attrib;
                        if (!currentAssemblies.Contains<PluginAssemblyAttribute>(file.CodeBase, out name, out attrib))
                        {
                            if (attrib != null)
                            {
                                currentAssemblies.Add(AppDomain.CurrentDomain.Load(name));
                                string directory = Path.GetDirectoryName(file.CodeBase);
                                if (AppDomain.CurrentDomain.SetupInformation.PrivateBinPath != null 
                                    && !AppDomain.CurrentDomain.SetupInformation.PrivateBinPath.Contains(directory + ";"))
                                {
                                    AppDomain.CurrentDomain.SetupInformation.PrivateBinPath += directory + ";";
                                }
                            }
                        }
                        file.IsValid = attrib != null;
                        file.LoadedAssembly = currentAssemblies
                            .Where(ca => ca.GetName().FullName.Equals(name.FullName, StringComparison.InvariantCultureIgnoreCase))
                            .FirstOrDefault();
                    }
                }
            }
        }
    }

    public static class AssemblyListEx
    {
        public static bool Contains<T>(this List<Assembly> assemblies, string test, out AssemblyName name, out T attribute) where T : Attribute
        {
            attribute = null;

            Uri testUri = new Uri(test);
            Assembly found = assemblies.Where(
                a => !a.IsDynamic
                    && a.CodeBase.Equals(testUri.AbsoluteUri, StringComparison.InvariantCultureIgnoreCase)).FirstOrDefault();
            if (found == null)
            {
                Assembly assembly = Assembly.ReflectionOnlyLoadFrom(test);
                name = null;
                name = assembly.GetName();
                for (int i = 0; i < assemblies.Count; i++)
                {
                    if (assemblies[i].FullName == assembly.FullName)
                    {
                        attribute = assembly.GetCustomAttributeReflectionOnly<T>();
                        return true;
                    }
                }

                attribute = assembly.GetCustomAttributeReflectionOnly<T>();
                return false;
            }
            else
            {
                Assembly assembly = found;
                name = assembly.GetName();
                attribute = assembly.GetCustomAttributeReflectionOnly<T>();
                return true;
            }
        }

        public static T GetCustomAttributeReflectionOnly<T>(this Assembly reflectionOnlyAssembly) where T : Attribute
        {
            CustomAttributeData cad = CustomAttributeData.GetCustomAttributes(reflectionOnlyAssembly)
                .Where(ca => ca.Constructor.DeclaringType.FullName.Equals(typeof(T).FullName)).FirstOrDefault();
            if (cad != null)
            {
                return (T)Activator.CreateInstance(typeof(T), new object[] { cad.ConstructorArguments[0].Value });
            }
            return null;
        }

    }
}

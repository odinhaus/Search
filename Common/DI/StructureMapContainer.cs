using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using StructureMap;
using Common.Application;
using Common.Licensing;
using Common.Exceptions;
using System.Reflection;
using StructureMap.Pipeline;
using System.Linq.Expressions;
using System.Collections;

namespace Common.DI
{
    public class StructureMapContainer : IContainer
    {
        public event ContainerChangedHandler ContainerChanged;

        public StructureMapContainer(StructureMap.IContainer container)
        {
            this.Container = container;
            // todo: loop thru and raise events for each type mapped in the container
        }

        public StructureMap.IContainer Container { get; private set; }


        public object GetInstance(Type pluginType, params object[] ctorArgs)
        {
            object instance;
            if (ctorArgs.Length > 0)
            {
                if (!TryGetInstanceWithArgs(ctorArgs, out instance))
                {
                    throw new InvalidOperationException("Cannot create provider for type " + pluginType.FullName);
                }
            }
            else
            {
                instance = this.Container.GetInstance(pluginType);
            }
            InitializeInstance(instance, pluginType.FullName);
            if (ApplyLicensing(instance))
                return instance;
            else
                throw new LicensingException();
        }

        public object GetInstance(Type pluginType, string instanceKey, params object[] ctorArgs)
        {
            object instance;
            if (ctorArgs.Length > 0)
            {
                if (!TryGetInstanceWithArgs(ctorArgs, out instance))
                {
                    throw new InvalidOperationException("Cannot create provider for type " + pluginType.FullName);
                }
            }
            else
            {
                instance = this.Container.GetInstance(pluginType, instanceKey);
            }
            InitializeInstance(instance, instanceKey);
            if (ApplyLicensing(instance))
                return instance;
            else
                throw new LicensingException();
        }

        public T GetInstance<T>(params object[] ctorArgs)
        {
            T instance;
            if (ctorArgs.Length > 0)
            {
                if (!TryGetInstanceWithArgs<T>(ctorArgs, out instance))
                {
                    throw new InvalidOperationException("Cannot create provider for type " + typeof(T).FullName);
                }
            }
            else
            {
                instance = this.Container.GetInstance<T>();
            }

            InitializeInstance(instance, typeof(T).FullName);
            if (ApplyLicensing(instance))
                return instance;
            else
                throw new LicensingException();
        }

        public T GetInstance<T>(string instanceKey, params object[] ctorArgs)
        {
            T instance;
            if (ctorArgs.Length > 0)
            {
                if (!TryGetInstanceWithArgs<T>(ctorArgs, out instance))
                {
                    throw new InvalidOperationException("Cannot create provider for type " + typeof(T).FullName);
                }
            }
            else
            {
                instance = this.Container.GetInstance<T>(instanceKey);
            }
            InitializeInstance(instance, instanceKey);
            if (ApplyLicensing(instance))
                return instance;
            else
                throw new LicensingException();
        }

        public IEnumerable<T> GetAllInstances<T>(params object[] ctorArgs)
        {
            IEnumerable<T> instances = null;
            if (ctorArgs.Length > 0)
            {
                if (!TryGetInstancesWithArgs<T>(ctorArgs, out instances))
                {
                    throw new InvalidOperationException("Cannot create provider for type " + typeof(T).FullName);
                }
            }
            else
            {
                instances = this.Container.GetAllInstances<T>();
            }

            foreach(var instance in instances)
            {
                InitializeInstance(instance, typeof(T).FullName);
                if (!ApplyLicensing(instance))
                {
                    throw new LicensingException();
                }
            }

            return instances;

        }


        public IEnumerable GetAllInstances(Type pluginType, params object[] ctorArgs)
        {
            IEnumerable instances = null;
            if (ctorArgs.Length > 0)
            {
                if (!TryGetInstancesWithArgs(pluginType, ctorArgs, out instances))
                {
                    throw new InvalidOperationException("Cannot create provider for type " + pluginType.FullName);
                }
            }
            else
            {
                instances = this.Container.GetAllInstances(pluginType);
            }

            foreach (var instance in instances)
            {
                InitializeInstance(instance, pluginType.FullName);
                if (!ApplyLicensing(instance))
                {
                    throw new LicensingException();
                }
            }

            return instances;

        }

        private bool TryGetInstanceWithArgs(Type pluginType, object[] ctorArgs, out object instance)
        {
            instance = null;
            if (this.Container.Model.HasDefaultImplementationFor(pluginType))
            {
                var type = this.Container.Model.DefaultTypeFor(pluginType);
                ConstructorInfo ctor = null;
                foreach (var c in type.GetConstructors(BindingFlags.Public | BindingFlags.Instance))
                {
                    if (TryMatchCtor(c, ctorArgs))
                    {
                        ctor = c;
                        break;
                    }
                }
                if (ctor != null)
                {
                    var ctorArgsDict = new Dictionary<string, object>();
                    var ctorParms = ctor.GetParameters();
                    for (int i = 0; i < ctorArgs.Length; i++)
                    {
                        ctorArgsDict.Add(ctorParms[i].Name, ctorArgs[i]);
                    }
                    instance = this.Container.GetInstance(pluginType, new ExplicitArguments(ctorArgsDict));
                }
            }

            return false;
        }

        private bool TryGetInstancesWithArgs(Type pluginType, object[] ctorArgs, out IEnumerable instances)
        {
            instances = null;
            if (this.Container.Model.HasDefaultImplementationFor(pluginType))
            {
                var type = this.Container.Model.DefaultTypeFor(pluginType);
                ConstructorInfo ctor = null;
                foreach (var c in type.GetConstructors(BindingFlags.Public | BindingFlags.Instance))
                {
                    if (TryMatchCtor(c, ctorArgs))
                    {
                        ctor = c;
                        break;
                    }
                }
                if (ctor != null)
                {
                    var ctorArgsDict = new Dictionary<string, object>();
                    var ctorParms = ctor.GetParameters();
                    for (int i = 0; i < ctorArgs.Length; i++)
                    {
                        ctorArgsDict.Add(ctorParms[i].Name, ctorArgs[i]);
                    }
                    instances = this.Container.GetAllInstances(pluginType, new ExplicitArguments(ctorArgsDict));
                }
            }

            return false;
        }

        private bool TryGetInstanceWithArgs<T>(object[] ctorArgs, out T instance)
        {
            instance = default(T);
            if (this.Container.Model.HasDefaultImplementationFor<T>())
            {
                var type = this.Container.Model.DefaultTypeFor<T>();
                ConstructorInfo ctor = null;
                foreach (var c in type.GetConstructors(BindingFlags.Public | BindingFlags.Instance))
                {
                    if (TryMatchCtor(c, ctorArgs))
                    {
                        ctor = c;
                        break;
                    }
                }
                if (ctor != null)
                {
                    var ctorArgsDict = new Dictionary<string, object>();
                    var ctorParms = ctor.GetParameters();
                    for(int i = 0; i < ctorArgs.Length; i++)
                    {
                        ctorArgsDict.Add(ctorParms[i].Name, ctorArgs[i]);
                    }
                    instance = this.Container.GetInstance<T>(new ExplicitArguments(ctorArgsDict));
                    return true;
                }
            }

            return false;
        }

        private bool TryGetInstancesWithArgs<T>(object[] ctorArgs, out IEnumerable<T> instances)
        {
            instances = null;
            if (this.Container.Model.HasDefaultImplementationFor<T>())
            {
                var type = this.Container.Model.DefaultTypeFor<T>();
                ConstructorInfo ctor = null;
                foreach (var c in type.GetConstructors(BindingFlags.Public | BindingFlags.Instance))
                {
                    if (TryMatchCtor(c, ctorArgs))
                    {
                        ctor = c;
                        break;
                    }
                }
                if (ctor != null)
                {
                    var ctorArgsDict = new Dictionary<string, object>();
                    var ctorParms = ctor.GetParameters();
                    for (int i = 0; i < ctorArgs.Length; i++)
                    {
                        ctorArgsDict.Add(ctorParms[i].Name, ctorArgs[i]);
                    }
                    instances = this.Container.GetAllInstances<T>(new ExplicitArguments(ctorArgsDict));
                    return true;
                }
            }

            return false;
        }

        private bool TryMatchCtor(ConstructorInfo c, object[] ctorArgs)
        {
            var parms = c.GetParameters();
            if (parms.Length == ctorArgs.Length)
            {
                for(int i = 0; i < parms.Length; i++)
                {
                    if (!TryCast(parms[i].ParameterType, ctorArgs[i]))
                        return false;
                }
            }
            return true;
        }

        private bool TryCast(Type parameterType, object v)
        {
            var parmExp = Expression.Parameter(typeof(object));
            var castExp = Expression.Convert(parmExp, parameterType);
            var func = typeof(Func<,>).MakeGenericType(typeof(object), parameterType);
            var lambda = Expression.Lambda(func, castExp, parmExp).Compile();
            try
            {
                lambda.DynamicInvoke(v);
                return true;
            }
            catch
            {
                return false;
            }
        }

        public void Map(Type sourceType, Type targetType)
        {
            this.Container.Configure(c => c.For(targetType).Use(sourceType));
        }

        public void Map(object sourceInstance, Type targetType)
        {
            this.Container.Configure(c => c.For(targetType).Use(sourceInstance));
        }

        public void Map<T, U>() where U : T
        {
            this.Container.Configure(c => c.For<T>().Use<U>());
        }

        public void Map<T>(T instance) where T : class
        {
            if (!InitializeInstance(instance, typeof(T).FullName))
            {
                this.Container.Configure(c => c.For<T>().Use(instance));
            }
        }

        public void Map<T>(T instance, string key) where T : class
        {
            if (!InitializeInstance(instance, key))
            {
                this.Container.Configure(c => c.For<T>().Use(instance).Named(key));
            }
        }

        static HashSet<object> _init = new HashSet<object>();
        protected virtual bool InitializeInstance(object instance, string name)
        {
            lock(_init)
            {
                if (!_init.Contains(instance)) // prevents multiple re-entries to initializers, causing stack overflows
                {
                    _init.Add(instance);
                    bool isRegistered = false;
                    if (instance is IInitialize && !((IInitialize)instance).IsRegistered)
                    {
                        var mappings = new ContainerMappings();
                        ((IInitialize)instance).Register(mappings); // register for service resolution

                        CreateMappings(mappings);
                    }

                    if (instance is IInitialize && !((IInitialize)instance).IsInitialized)
                    {
                        ((IInitialize)instance).Initialize(name, AppContext.Current.Setup.Args);
                        isRegistered = true;
                    }
                    if (instance is IInstaller && !((IInstaller)instance).IsInstalled)
                    {
                        var attrib = instance.GetType().GetCustomAttributes(true).OfType<InstallerPluginAttribute>().FirstOrDefault();
                        string[] appNames = null;
                        if (attrib != null)
                            appNames = attrib.Apps;
                        ((IInstaller)instance).Install(appNames);
                    }
                    _init.Remove(instance); // we're done, so remove it
                    return isRegistered;
                }
                else return true;
            }
        }

        protected virtual bool ApplyLicensing(object instance)
        {
            lock (_init)
            {
                if (!_init.Contains(instance)) // prevents multiple re-entries to initializers, causing stack overflows
                {
                    try
                    {
                        _init.Add(instance);
                        if (instance is ILicensedPlugin)
                        {
                            ((ILicensedPlugin)instance).ApplyLicensing(AppContext.Current.LicenseManager.GetLicenses(), AppContext.Current.Setup.Args);
                            return ((ILicensedPlugin)instance).IsLicensed(instance);
                        }
                        else return true;
                    }
                    finally
                    {
                        _init.Remove(instance); // we're done, so remove it
                    }
                }
                else return true;
            }
        }

        private void CreateMappings(ContainerMappings mappings)
        {
            foreach (var mapping in mappings)
            {
                if (mapping.ImplementationObject == null && !string.IsNullOrEmpty(mapping.InstanceKey))
                    this.Container.Configure(c => c.For(mapping.TargetType).Use(mapping.ImplementationType).Named(mapping.InstanceKey));
                else if (mapping.ImplementationObject != null && !string.IsNullOrEmpty(mapping.InstanceKey))
                    this.Container.Configure(c => c.For(mapping.TargetType).Use(mapping.ImplementationObject).Named(mapping.InstanceKey));
                else if (!string.IsNullOrEmpty(mapping.InstanceKey))
                    this.Container.Configure(c => c.For(mapping.TargetType).Use(mapping.InstanceKey));
                else if (mapping.ImplementationObject == null && string.IsNullOrEmpty(mapping.InstanceKey))
                    this.Container.Configure(c => c.For(mapping.TargetType).Use(mapping.ImplementationType));
                else if (mapping.ImplementationObject != null)
                    this.Container.Configure(c => c.For(mapping.TargetType).Use(mapping.ImplementationObject));
                else if (mapping.ImplementationType == null)
                    this.Container.Configure(c => c.For(mapping.TargetType).Use(mapping.TargetType));
            }
        }
    }
}

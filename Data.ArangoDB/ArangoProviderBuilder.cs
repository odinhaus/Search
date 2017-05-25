using Common.Web.Handlers;
using Data.Core;
using Data.Core.ComponentModel;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Data.ArangoDB
{
    public class ArangoProviderBuilder : IModelListProviderBuilder, IModelPersistenceProviderBuilder, IModelQueryProviderBuilder, IModelQueueProviderBuilder
    {
        static Dictionary<string, Type> _buildCache = new Dictionary<string, Type>();
        private static AssemblyName _asmName = new AssemblyName() { Name = "Data.Runtime.Provider" };
        private static ModuleBuilder _modBuilder;
        private static AssemblyBuilder _asmBuilder;

        static ArangoProviderBuilder()
        {
            _asmBuilder = Thread.GetDomain().DefineDynamicAssembly(_asmName, AssemblyBuilderAccess.RunAndSave);
            _modBuilder = _asmBuilder.DefineDynamicModule(_asmName.Name, _asmName.Name + ".dll", true);
        }

        public IModelListProvider<T> CreateListProvider<T>()
        {
            return (IModelListProvider<T>)CreateListProvider(typeof(T));
        }

        public object CreateListProvider(Type modelType)
        {
            var type = CreateListProviderType(modelType, typeof(IModelListProvider<>).MakeGenericType(modelType), typeof(ModelListProviderBase<>).MakeGenericType(modelType));
            return Activator.CreateInstance(type);
        }


        public static Type CreateListProviderType<T>(Type baseType = null, bool loadFromCache = true)
        {
            return CreateListProviderType(typeof(T), typeof(IModelListProvider<>).MakeGenericType(typeof(T)), baseType, loadFromCache);
        }

        public object CreatePersistenceProvider(Type modelType)
        {
            var type = CreatePersistenceProviderType(modelType, typeof(IModelPersistenceProvider<>).MakeGenericType(modelType), typeof(ModelPersistenceProviderBase<>).MakeGenericType(modelType));
            return Activator.CreateInstance(type);
        }

        public IModelPersistenceProvider<T> CreatePersistenceProvider<T>() where T : IModel
        {
            return (IModelPersistenceProvider<T>)CreatePersistenceProvider(typeof(T));
        }

        public object CreateQueryProvider(Type modelType)
        {
            var type = CreateQueryProviderType(modelType, typeof(IModelQueryProvider<>).MakeGenericType(modelType), typeof(ModelQueryProviderBase<>).MakeGenericType(modelType));
            return Activator.CreateInstance(type);
        }

        public IModelQueryProvider<T> CreateQueryProvider<T>() where T : IModel
        {
            return (IModelQueryProvider<T>)CreateQueryProvider(typeof(T));
        }

        public static Type CreateQueryProviderType(Type modelType, Type interfaceType, Type baseType, bool loadFromCache = true)
        {
            if (!interfaceType.IsInterface) throw new InvalidOperationException("The generic parameter type T must be an interface.");

            Type instanceType = null;
            var className = "Dyn_" + interfaceType.FullName.Replace(".", "_");
            lock (_buildCache)
            {
                if (!loadFromCache || !_buildCache.TryGetValue(className, out instanceType))
                {

                    var typeBuilder = _modBuilder.DefineType(className, TypeAttributes.Public | TypeAttributes.Class | TypeAttributes.Serializable);
                    typeBuilder.AddInterfaceImplementation(interfaceType);
                    if (baseType != null)
                        typeBuilder.SetParent(baseType);


                    var modelName = ((ModelAttribute)modelType.GetCustomAttribute(typeof(ModelAttribute))).ModelName;

                    typeBuilder.SetCustomAttribute(
                        new CustomAttributeBuilder(
                            typeof(ServiceProviderAttribute).GetConstructor(new Type[] { typeof(string), typeof(Type) }),
                            new object[] { modelName, interfaceType })); // get model name from ModelAttribute on model type))

                    BuildDefaultCtor(interfaceType, typeBuilder);

                    instanceType = typeBuilder.CreateType();
                    if (_buildCache.ContainsKey(className))
                    {
                        _buildCache[className] = instanceType;
                    }
                    else
                    {
                        _buildCache.Add(className, instanceType);
                    }
                }
                else
                {
                    instanceType = _buildCache[className];
                }
            }
            return instanceType;
        }


        public object CreateQueueProvider(Type modelType)
        {
            var type = CreateQueueProviderType(modelType, typeof(IModelQueueProvider<>).MakeGenericType(modelType), typeof(ModelQueueProviderBase<>).MakeGenericType(modelType));
            return Activator.CreateInstance(type);
        }

        public IModelQueueProvider<T> CreateQueueProvider<T>() where T : IModel
        {
            return (IModelQueueProvider<T>)CreateQueryProvider(typeof(T));
        }

        public static Type CreateQueueProviderType(Type modelType, Type interfaceType, Type baseType, bool loadFromCache = true)
        {
            if (!interfaceType.IsInterface) throw new InvalidOperationException("The generic parameter type T must be an interface.");

            Type instanceType = null;
            var className = "Dyn_" + interfaceType.FullName.Replace(".", "_");
            lock (_buildCache)
            {
                if (!loadFromCache || !_buildCache.TryGetValue(className, out instanceType))
                {

                    var typeBuilder = _modBuilder.DefineType(className, TypeAttributes.Public | TypeAttributes.Class | TypeAttributes.Serializable);
                    typeBuilder.AddInterfaceImplementation(interfaceType);
                    if (baseType != null)
                        typeBuilder.SetParent(baseType);


                    var modelName = ((ModelAttribute)modelType.GetCustomAttribute(typeof(ModelAttribute))).ModelName;

                    typeBuilder.SetCustomAttribute(
                        new CustomAttributeBuilder(
                            typeof(ServiceProviderAttribute).GetConstructor(new Type[] { typeof(string), typeof(Type) }),
                            new object[] { modelName, interfaceType })); // get model name from ModelAttribute on model type))

                    BuildDefaultCtor(interfaceType, typeBuilder);

                    instanceType = typeBuilder.CreateType();
                    if (_buildCache.ContainsKey(className))
                    {
                        _buildCache[className] = instanceType;
                    }
                    else
                    {
                        _buildCache.Add(className, instanceType);
                    }
                }
                else
                {
                    instanceType = _buildCache[className];
                }
            }
            return instanceType;
        }

        public static Type CreatePersistenceProviderType(Type modelType, Type interfaceType, Type baseType, bool loadFromCache = true)
        {
            if (!interfaceType.IsInterface) throw new InvalidOperationException("The generic parameter type T must be an interface.");

            Type instanceType = null;
            var className = "Dyn_" + interfaceType.FullName.Replace(".", "_");
            lock (_buildCache)
            {
                if (!loadFromCache || !_buildCache.TryGetValue(className, out instanceType))
                {

                    var typeBuilder = _modBuilder.DefineType(className, TypeAttributes.Public | TypeAttributes.Class | TypeAttributes.Serializable);
                    typeBuilder.AddInterfaceImplementation(interfaceType);
                    if (baseType != null)
                        typeBuilder.SetParent(baseType);


                    var modelName = ((ModelAttribute)modelType.GetCustomAttribute(typeof(ModelAttribute))).ModelName;

                    typeBuilder.SetCustomAttribute(
                        new CustomAttributeBuilder(
                            typeof(ServiceProviderAttribute).GetConstructor(new Type[] { typeof(string), typeof(Type) }),
                            new object[] { modelName, interfaceType })); // get model name from ModelAttribute on model type))

                    BuildDefaultCtor(interfaceType, typeBuilder);

                    instanceType = typeBuilder.CreateType();
                    if (_buildCache.ContainsKey(className))
                    {
                        _buildCache[className] = instanceType;
                    }
                    else
                    {
                        _buildCache.Add(className, instanceType);
                    }
                }
                else
                {
                    instanceType = _buildCache[className];
                }
            }
            return instanceType;
        }

        public static Type CreateListProviderType(Type modelType, Type interfaceType, Type baseType = null, bool loadFromCache = true)
        {
            if (!interfaceType.IsInterface) throw new InvalidOperationException("The generic parameter type T must be an interface.");

            Type instanceType = null;
            var className = "Dyn_" + interfaceType.FullName.Replace(".", "_");
            lock (_buildCache)
            {
                if (!loadFromCache || !_buildCache.TryGetValue(className, out instanceType))
                {

                    var typeBuilder = _modBuilder.DefineType(className, TypeAttributes.Public | TypeAttributes.Class | TypeAttributes.Serializable);
                    typeBuilder.AddInterfaceImplementation(interfaceType);
                    if (baseType != null)
                        typeBuilder.SetParent(baseType);


                    var modelName = ((ModelAttribute)modelType.GetCustomAttribute(typeof(ModelAttribute))).ModelName;

                    typeBuilder.SetCustomAttribute(
                        new CustomAttributeBuilder(
                            typeof(ServiceProviderAttribute).GetConstructor(new Type[] { typeof(string), typeof(Type) }),
                            new object[] { modelName, interfaceType })); // get model name from ModelAttribute on model type))
                    
                    BuildDefaultCtor(interfaceType, typeBuilder);

                    instanceType = typeBuilder.CreateType();
                    if (_buildCache.ContainsKey(className))
                    {
                        _buildCache[className] = instanceType;
                    }
                    else
                    {
                        _buildCache.Add(className, instanceType);
                    }
                }
                else
                {
                    instanceType = _buildCache[className];
                }
            }
            return instanceType;
        }

        private Type CreateTraversalProviderType(Type modelType, Type interfaceType, Type baseType, bool loadFromCache = true)
        {
            if (!interfaceType.IsInterface) throw new InvalidOperationException("The generic parameter type T must be an interface.");

            Type instanceType = null;
            var className = "Dyn_" + interfaceType.FullName.Replace(".", "_");
            lock (_buildCache)
            {
                if (!loadFromCache || !_buildCache.TryGetValue(className, out instanceType))
                {

                    var typeBuilder = _modBuilder.DefineType(className, TypeAttributes.Public | TypeAttributes.Class | TypeAttributes.Serializable);
                    typeBuilder.AddInterfaceImplementation(interfaceType);
                    if (baseType != null)
                        typeBuilder.SetParent(baseType);


                    var modelName = ((ModelAttribute)modelType.GetCustomAttribute(typeof(ModelAttribute))).ModelName;

                    typeBuilder.SetCustomAttribute(
                        new CustomAttributeBuilder(
                            typeof(ServiceProviderAttribute).GetConstructor(new Type[] { typeof(string), typeof(Type) }),
                            new object[] { modelName, interfaceType })); // get model name from ModelAttribute on model type))

                    BuildDefaultCtor(interfaceType, typeBuilder);

                    instanceType = typeBuilder.CreateType();
                    if (_buildCache.ContainsKey(className))
                    {
                        _buildCache[className] = instanceType;
                    }
                    else
                    {
                        _buildCache.Add(className, instanceType);
                    }
                }
                else
                {
                    instanceType = _buildCache[className];
                }
            }
            return instanceType;
        }

        private static void BuildDefaultCtor(Type instanceType, TypeBuilder typeBuilder)
        {
            var ctor = typeBuilder.DefineDefaultConstructor(MethodAttributes.Public
                | MethodAttributes.HideBySig
                | MethodAttributes.SpecialName
                | MethodAttributes.RTSpecialName);
        }
    }
}

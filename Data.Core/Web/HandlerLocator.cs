using Common.Web.Handlers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net.Http;
using Common;
using System.Reflection;
using Data.Core;
using Common.Diagnostics;
using Data.Core.Security;
using Data.Core.Auditing;
using Data.Core.Templating;

namespace Data.Core.Web
{
    public class HandlerLocator : ILocateHandlers
    {
        static Dictionary<string, Type> _mappings = null;
        static Dictionary<string, Type> _models = null;
        static object _sync = new object();

        static HandlerLocator()
        {
            CreateMappings();
        }

        private static void CreateMappings()
        {
            lock (_sync)
            {
                _mappings = new Dictionary<string, Type>();
                _models = new Dictionary<string, Type>();

                foreach (var asm in AppContext.Current?.Apps.SelectMany(app => app.Manifest.Targets)
                    .SelectMany(target => target.Files)
                    .Where(file => file.Reflect)
                    .Select(file => file.LoadedAssembly) ?? AppDomain.CurrentDomain.GetAssemblies())
                {
                    if (asm == null) continue;

                    foreach (var type in asm.GetTypes())
                    {
                        CreateMapping(type);
                    }
                }
            }
        }

        private static void CreateMapping(Type type)
        {
            var attrib = (ServiceProviderAttribute[])type.GetCustomAttributes(typeof(ServiceProviderAttribute), true);
            var hasName = attrib.Length > 0;

            if (!hasName)
            {
                // check if its a model type
                var modelAttrib = (ModelAttribute)type.GetCustomAttributes(typeof(ModelAttribute), true).FirstOrDefault();
                if (modelAttrib == null || !modelAttrib.IsPublic) return; // its not a service type or model type or the model is not public, so bail
                try
                {
                    _models.Add(modelAttrib.FullyQualifiedName, type);
                }
                catch (Exception ex)
                {
                    Logger.LogError(ex, "Could not add Model Type: " + type.FullName, true);
                    throw ex;
                }
            }

            foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static))
            {
                var caAction = (ServiceProviderActionAttribute[])method.GetCustomAttributes(typeof(ServiceProviderActionAttribute), true);
                var hasAction = caAction.Length > 0;

                if (hasName || hasAction)
                {
                    CreateMappings(type, attrib, method, caAction);
                }
            }
        }

        private static void CreateMappings(Type type, ServiceProviderAttribute[] attribs, MethodInfo method, ServiceProviderActionAttribute[] actions)
        {
            if (attribs.Length > 0)
            {
                foreach (var attrib in attribs)
                {
                    if (actions.Length > 0)
                    {
                        foreach (var caAction in actions)
                        {
                            var key = string.Format("{0}/{1}", attrib.Name, caAction.Name);
                            if (!_mappings.ContainsKey(key))
                                _mappings.Add(key, attrib.ServiceType);
                        }
                    }
                    else
                    {
                        var key = string.Format("{0}/{1}", attrib.Name, method.Name);
                        if (!_mappings.ContainsKey(key))
                            _mappings.Add(key, attrib.ServiceType);
                    }
                }
            }
            else
            {
                if (actions.Length > 0)
                {
                    foreach (var action in actions)
                    {
                        var key = string.Format("{0}/{1}", type.Name, action.Name);
                        if (!_mappings.ContainsKey(key))
                            _mappings.Add(key, type);
                    }
                }
            }
        }
        public void Map(Type handlerType)
        {
            lock (_mappings)
            {
                CreateMapping(handlerType);
            }
        }

        public void Map(string serviceName, string actionName, Type handlerType)
        {
            lock (_mappings)
            {
                _mappings.Add(string.Format("{0}/{1}", serviceName, actionName), handlerType);
            }
        }


        public bool Locate(HttpRequestMessage request, out IDelegatingHandler handler)
        {
            handler = null;
            if (request.RequestUri.Segments.Length < 2) return false;
            var service = request.RequestUri.Segments[request.RequestUri.Segments.Length - 2].Replace("/", "");
            var action = request.RequestUri.Segments[request.RequestUri.Segments.Length - 1].Replace("/", "");

            if (request.RequestUri.PathAndQuery.ToLower().Contains("/token"))
            {
                service = "/token";
                action = null;
            }
            else if (request.RequestUri.PathAndQuery.ToLower().Contains("/api"))
            {
                service = "/api";
                action = null;
            }

            return Locate(service, action, out handler);
        }

        public bool Locate(string service, string action, out IDelegatingHandler handler)
        {
            handler = null;
            var key = string.Format("{0}/{1}", service, action);

            lock (_mappings)
            {
                Type type;
                if (_mappings.TryGetValue(key, out type))
                {
                    var provider = AppContext.Current.Container.GetInstance(type);
                    handler = new ServiceProviderHandler(provider);
                    return true;
                }
                else if (service == "/token")
                {
                    handler = new AuthenticationHandler();
                    return true;
                }
                else if (service == "/api")
                {
                    handler = new ApiServiceListingHandler(_mappings.Values.Distinct().ToArray(), _models.Values.Distinct().ToArray());
                    return true;
                }
                else
                {
                    try
                    {
                        Type modelType;
                        var found = _models.TryGetValue(service, out modelType);
                        if (found
                            && action.Equals("Find", StringComparison.CurrentCultureIgnoreCase))
                        {
                            // we have a generic Model List request, with no handler
                            // create the handler, map it, and return it
                            var builder = AppContext.Current.Container.GetInstance<IModelListProviderBuilder>();
                            if (builder != null)
                            {
                                var listProvider = builder.CreateListProvider(modelType);
                                this.Map(listProvider.GetType()); // map the created provider
                                AppContext.Current.Container.Map(listProvider.GetType(), typeof(IModelListProvider<>).MakeGenericType(modelType));
                                handler = new ServiceProviderHandler(listProvider);
                                return true;
                            }
                        }
                        else if (found
                            && action.Equals("GetClaims", StringComparison.CurrentCultureIgnoreCase))
                        {
                            var provider = AppContext.Current.Container.GetInstance<IUserAuthorizationProvider>();
                            handler = new ServiceProviderHandler(provider);
                            return true;
                        }
                        else if (found
                            && (action.Equals("Create", StringComparison.CurrentCultureIgnoreCase)
                            || action.Equals("Save", StringComparison.CurrentCultureIgnoreCase)
                            || action.Equals("Update", StringComparison.CurrentCultureIgnoreCase)
                            || action.Equals("Get", StringComparison.CurrentCultureIgnoreCase)
                            || action.Equals("Delete", StringComparison.CurrentCultureIgnoreCase)
                            || action.Equals("Lock", StringComparison.CurrentCultureIgnoreCase)
                            || action.Equals("Unlock", StringComparison.CurrentCultureIgnoreCase)))
                        {
                            // we have a generic Model List request, with no handler
                            // create the handler, map it, and return it
                            var builder = AppContext.Current.Container.GetInstance<IModelPersistenceProviderBuilder>();
                            if (builder != null)
                            {
                                var peristenceProvider = builder.CreatePersistenceProvider(modelType);
                                this.Map(peristenceProvider.GetType()); // map the created provider
                                AppContext.Current.Container.Map(peristenceProvider.GetType(), typeof(IModelPersistenceProvider<>).MakeGenericType(modelType));
                                handler = new ServiceProviderHandler(peristenceProvider);
                                return true;
                            }
                        }
                        else if (found
                            && action.Equals("View", StringComparison.CurrentCultureIgnoreCase))
                        {
                            // we have a generic Model List request, with no handler
                            // create the handler, map it, and return it

                            var builder = AppContext.Current.Container.GetInstance<IModelTemplateProviderBuilder>();
                            if (builder != null)
                            {
                                var templateProvider = builder.CreateTemplateProvider(modelType);
                                this.Map(templateProvider.GetType()); // map the created provider
                                AppContext.Current.Container.Map(templateProvider.GetType(), typeof(IModelTemplateProvider<>).MakeGenericType(modelType));
                                handler = new ServiceProviderHandler(templateProvider);
                                return true;
                            }
                        }
                        else if (found
                            && action.Equals("Query", StringComparison.CurrentCultureIgnoreCase))
                        {
                            // we have a generic Model List request, with no handler
                            // create the handler, map it, and return it

                            var builder = AppContext.Current.Container.GetInstance<IModelQueryProviderBuilder>();
                            if (builder != null)
                            {
                                var traversalProvider = builder.CreateQueryProvider(modelType);
                                this.Map(traversalProvider.GetType()); // map the created provider
                                AppContext.Current.Container.Map(traversalProvider.GetType(), typeof(IModelQueryProvider<>).MakeGenericType(modelType));
                                handler = new ServiceProviderHandler(traversalProvider);
                                return true;
                            }
                        }
                        else if (found
                            && (action.Equals("QueuedCount", StringComparison.CurrentCultureIgnoreCase)
                            || action.Equals("Hold", StringComparison.CurrentCultureIgnoreCase))
                            || action.Equals("Release", StringComparison.CurrentCultureIgnoreCase)
                            || action.Equals("Dequeue", StringComparison.CurrentCultureIgnoreCase)
                            || action.Equals("Peek", StringComparison.CurrentCultureIgnoreCase))
                        {
                            // we have a generic Model List request, with no handler
                            // create the handler, map it, and return it
                            var builder = AppContext.Current.Container.GetInstance<IModelQueueProviderBuilder>();
                            if (builder != null)
                            {
                                var queueProvider = builder.CreateQueueProvider(modelType);
                                this.Map(queueProvider.GetType()); // map the created provider
                                AppContext.Current.Container.Map(queueProvider.GetType(), typeof(IModelQueueProvider<>).MakeGenericType(modelType));
                                handler = new ServiceProviderHandler(queueProvider);
                                return true;
                            }
                        }
                        else if (found
                            && (action.Equals("History", StringComparison.CurrentCultureIgnoreCase)))
                        {
                            // we have a generic Model List request, with no handler
                            // create the handler, map it, and return it
                            var auditer = AppContext.Current.Container.GetInstance<IAuditer>();
                            if (auditer != null)
                            {
                                this.Map(typeof(IAuditer)); // map the created provider
                                handler = new ServiceProviderHandler(auditer);
                                return true;
                            }
                        }


                        return false;
                    }
                    catch (TargetInvocationException te)
                    {
                        if (te.InnerException.InnerException == null)
                            throw te.InnerException;
                        else
                            throw te.InnerException.InnerException;
                    }
                    catch (TypeInitializationException tie)
                    {
                        throw tie.InnerException;
                    }
                }
            }
        }
    }
}

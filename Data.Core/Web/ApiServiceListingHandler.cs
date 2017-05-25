using Common;
using Common.Web.Handlers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Data.Core.Web
{
    public class ApiServiceListingHandler : IDelegatingHandler
    {
        private Type[] serviceTypes;
        private Type[] modelTypes;

        public ApiServiceListingHandler(Type[] type1, Type[] type2)
        {
            this.serviceTypes = type1;
            this.modelTypes = type2;
        }

        public async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var sb = new StringBuilder();
            var path = "\"POST /{0}/{1}{{{2}}}\"";

            sb.AppendLine("{\"apis\": [");
            bool notFirst = false;
            foreach (var type in serviceTypes)
            {
                var inst = AppContext.Current.Container.GetInstance(type);
                var methods = type.GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                var service = (inst.GetType().GetCustomAttribute(typeof(ServiceProviderAttribute)) as ServiceProviderAttribute).Name;
                for (int i = 0; i < methods.Length; i++)
                {
                    var method = methods[i];
                    if (notFirst) sb.AppendLine(", ");
                    notFirst = true;
                    var action = method.GetCustomAttribute<ServiceProviderActionAttribute>();
                    var name = method.Name;
                    if (action != null)
                    {
                        name = action.Name;
                    }
                    var parms = "";
                    foreach (var p in method.GetParameters())
                    {
                        if (parms.Length > 0)
                        {
                            parms += ", ";
                        }
                        parms += p.Name + ":";
                        if (p.ParameterType.Implements<IModel>())
                        {
                            parms += ModelTypeManager.GetModelName(p.ParameterType);
                        }
                        else
                        {
                            parms += p.ParameterType.Name;
                        }
                    }
                    sb.Append(string.Format(path, service, name, parms));
                }
            }

            foreach (var modelType in modelTypes)
            {
                var service = ModelTypeManager.GetModelName(modelType);
                var types = new Type[]
                {
                    typeof(IModelPersistenceProvider<>).MakeGenericType(modelType),
                    typeof(IModelQueryProvider<>).MakeGenericType(modelType),
                    typeof(IModelQueueProvider<>).MakeGenericType(modelType),
                    typeof(IAuditer)
                };

                foreach (var type in types)
                {
                    var methods = type.GetMethods(BindingFlags.Public | BindingFlags.Instance);
                    if (type == typeof(IAuditer))
                    {
                        methods = methods.Where(m => m.Name.Equals("History")).ToArray();
                    }
                    for (int i = 0; i < methods.Length; i++)
                    {
                        var method = methods[i];
                        if (notFirst) sb.AppendLine(", ");
                        notFirst = true;
                        var name = method.Name;
                        var parms = "";
                        var mParms = method.GetParameters();
                        foreach (var p in mParms)
                        {
                            if (parms.Length > 0)
                            {
                                parms += ", ";
                            }
                            parms += p.Name + ":";
                            if (p.ParameterType.Implements<IModel>())
                            {
                                parms += ModelTypeManager.GetModelName(p.ParameterType);
                            }
                            else
                            {
                                parms += p.ParameterType.Name;
                            }
                        }
                        sb.Append(string.Format(path, service, name, parms));

                        var overrides = method.GetCustomAttributes<OverrideAttribute>();
                        if (overrides.Count() > 0)
                        {
                            foreach (var o in overrides)
                            {
                                var oInstance = o.CreateOverride();
                                foreach (var p in mParms)
                                {
                                    if (parms.Length > 0)
                                    {
                                        parms += ", ";
                                    }
                                    parms += p.Name + ":";
                                    if (p.ParameterType.Implements<IModel>())
                                    {
                                        parms += ModelTypeManager.GetModelName(p.ParameterType);
                                    }
                                    else
                                    {
                                        parms += p.ParameterType.Name;
                                    }
                                }
                                foreach (var a in oInstance.ArgumentTypes)
                                {
                                    if (parms.Length > 0)
                                    {
                                        parms += ", ";
                                    }
                                    parms += a.Name + ":" + a.Type.Name;
                                }
                                sb.Append(string.Format(path, service, name, parms));
                            }
                        }
                    }
                }
            }

            sb.AppendLine("]}");
            return await Task.Run(() =>
            {
                var result = new HttpResponseMessage(System.Net.HttpStatusCode.OK)
                {
                    Content = new StringContent(sb.ToString())
                };
                return result;
            });
        }
    }
}

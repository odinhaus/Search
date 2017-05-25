using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace Common.Web.Handlers
{
    public interface ILocateHandlers
    {
        /// <summary>
        /// Maps a specific service name and action name to a service handler type
        /// </summary>
        /// <param name="serviceName"></param>
        /// <param name="actionName"></param>
        /// <param name="handlerType"></param>
        void Map(string serviceName, string actionName, Type handlerType);
        /// <summary>
        /// Examines a provided type for ServiceProviderAttribute and ServiceProviderActionAttributes to map handlers
        /// </summary>
        /// <param name="handlerType"></param>
        void Map(Type handlerType);
        /// <summary>
        /// Locates a handler for the given message
        /// </summary>
        /// <param name="request"></param>
        /// <param name="handler"></param>
        /// <returns></returns>
        bool Locate(HttpRequestMessage request, out IDelegatingHandler handler);
    }
}

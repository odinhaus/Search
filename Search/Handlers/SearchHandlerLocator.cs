using Common.Web.Handlers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net.Http;
using Common;
using Data.Core.Web;

namespace Suffuz.Handlers
{
    public class SearchHandlerLocator : ILocateHandlers
    {
        public bool Locate(HttpRequestMessage request, out IDelegatingHandler handler)
        {
            var formData = request.Content.ReadAsFormDataAsync().Result;

            if (formData?.AllKeys.Any(key => key.Equals("command", StringComparison.CurrentCultureIgnoreCase)) ?? false)
            {
                // slack command
                handler = new SearchHandler(formData["text"]);
                return true;
            }
            else if (request.RequestUri.ToString().StartsWith(AppContext.GetEnvironmentVariable("slack:redirect_uri","")))
            {
                // slack oAuth app authorization code callback
                handler = new AuthHandler();
                return true;
            }
            else if (new HandlerLocator().Locate(request, out handler))
            {
                // it's a data model access call
                return true;
            }
            else
            {
                // fallback to static files
                handler = new FileSystemHandler(AppContext.GetEnvironmentVariable<string>("WebRoot", System.IO.Directory.GetParent(AppDomain.CurrentDomain.BaseDirectory).Parent.FullName));
                return true;
            }
        }

        public void Map(Type handlerType)
        {
            throw new NotImplementedException();
        }

        public void Map(string serviceName, string actionName, Type handlerType)
        {
            throw new NotImplementedException();
        }
    }
}

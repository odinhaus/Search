using Common;
using Common.Web.Handlers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Suffuz.Handlers
{
    public class SearchRoutingHandler : DelegatingHandler
    {
        protected async override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var locator = AppContext.Current.Container.GetInstance<ILocateHandlers>();
            try
            {
                IDelegatingHandler handler;
                if (locator.Locate(request, out handler))
                {
                    HttpResponseMessage response;
                    if (request.Method == HttpMethod.Options)
                    {
                        response = new HttpResponseMessage(System.Net.HttpStatusCode.OK);
                    }
                    else
                    {
                        response = await Task.Run(() => handler.SendAsync(request, cancellationToken).Result);
                    }
                    response.Headers.Add("Access-Control-Allow-Origin", "*");
                    response.Headers.Add("Access-Control-Allow-Headers", "authorization,content-type");

                    return response;
                }
                else
                {
                    return await base.SendAsync(request, cancellationToken);
                }
            }
            catch(Exception e)
            {
                var ret = new HttpResponseMessage(System.Net.HttpStatusCode.Unauthorized);
                ret.Content = new StringContent("{ \"Message\": \"" + e.Message + "\" }");
                return ret;
            }
        }
    }
}

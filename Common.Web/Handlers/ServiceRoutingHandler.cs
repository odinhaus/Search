using Microsoft.Owin.Security;
using Microsoft.Owin.Security.DataHandler;
using Microsoft.Owin.Security.DataHandler.Encoder;
using Microsoft.Owin.Security.DataHandler.Serializer;
using Microsoft.Owin.Security.DataProtection;
using Microsoft.Owin.Security.Infrastructure;
using Microsoft.AspNet.Identity.Owin;
using Common.OWIN;
using Common.Security;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Owin.Security.OAuth;
using System.Runtime.Remoting.Messaging;
using System.Web;
using Common.Auditing;

namespace Common.Web.Handlers
{
    public class ServiceRoutingHandler : DelegatingHandler
    {
        protected async override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            HttpResponseMessage response = null;
            try
            {
                SecurityContext.Current.Logoff();
                var locator = AppContext.Current.Container.GetInstance<ILocateHandlers>();
                IDelegatingHandler handler;


                var auditScopeHandler = AppContext.Current.Container.GetInstance<IHttpAuditScopeTokenReader>();
                string scopeId;
                auditScopeHandler.ReadToken(request, out scopeId);


                var authTokenHandler = AppContext.Current.Container.GetInstance<IHttpHeaderAuthTokenBuilder>();
                string token;
                TokenType tokenType;
                if (authTokenHandler.ReadHeader(request, out token, out tokenType))
                {
                    var context = request.GetOwinContext();
                    CallContext.LogicalSetData("IOwinContext", context); // makes owin context available downstream

                    SecurityContext.Create<SecurityContextProviderServer>();
                    SecurityContext.Current.ScopeId = scopeId;
                    if (!SecurityContext.Current.Authenticate(token, tokenType))
                    {
                        throw new System.Security.SecurityException("The provided token is either invalid or has expired and cannot be used");
                    }
                }

                var sec = SecurityContext.Current.Clone();

                if (locator.Locate(request, out handler))
                {
                    if (request.Method == HttpMethod.Options)
                    {
                        response = new HttpResponseMessage(System.Net.HttpStatusCode.OK);
                    }
                    else
                    {
                        response = await sec.ExecuteAsync(() => handler.SendAsync(request, cancellationToken).Result);
                    }
                }
                else
                {
                    response = await sec.ExecuteAsync(() => base.SendAsync(request, cancellationToken).Result);
                }

                return response;
            }
            catch (System.Security.SecurityException se)
            {
                response = new HttpResponseMessage(System.Net.HttpStatusCode.Unauthorized);
                response.Content = new StringContent("{ \"Message\": \"" + se.Message + "\" }");

                return response;
            }
            catch (Exception ex)
            {
                response = new HttpResponseMessage(System.Net.HttpStatusCode.InternalServerError);
                response.Content = new StringContent("{ \"Message\": \"" + ex.Message + "\" }");

                return response;
            }
            finally
            {
                // always return cors
                response.Headers.Add("Access-Control-Allow-Origin", "*");
                response.Headers.Add("Access-Control-Allow-Headers", "authorization,content-type");
                SecurityContext.Current.Logoff();
            }
        }
    }

    public class MachineKeyProtector : IDataProtector
    {
        private readonly string[] _purpose =
        {
            typeof(OAuthAuthorizationServerMiddleware).Namespace,
            "Access_Token",
            "v1"
        };

        public byte[] Protect(byte[] userData)
        {
            //return new DpapiDataProtectionProvider().Create("ASP.NET Identity").Protect(userData);
            return System.Web.Security.MachineKey.Protect(userData, _purpose);
        }

        public byte[] Unprotect(byte[] protectedData)
        {
            //return new DpapiDataProtectionProvider().Create("ASP.NET Identity").Unprotect(protectedData);
            return System.Web.Security.MachineKey.Unprotect(protectedData, _purpose);
        }
    }
}

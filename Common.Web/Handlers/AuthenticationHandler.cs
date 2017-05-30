using Newtonsoft.Json;
using Common.Security;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Common.Web.Handlers
{
    public class AuthenticationHandler : IDelegatingHandler
    {
        public async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (SecurityContext.Current != null
                && SecurityContext.Current.CurrentPrincipal != null
                && SecurityContext.Current.CurrentPrincipal.Identity.IsAuthenticated)
            {
                // we logged in from the root handler, no need to do it again
                return await SecurityContext.Current.ExecuteAsync(() => CreateAuthSuccessResponse(SecurityContext.Current));
            }
            else if (SecurityContext.Current.CurrentPrincipal == null)
            {
                // we're not already logged in, so try to auth now
                var authTokenHandler = AppContext.Current.Container.GetInstance<IHttpHeaderAuthTokenBuilder>();
                string token;
                TokenType tokenType;
                if (authTokenHandler.ReadHeader(request, out token, out tokenType))
                {
                    SecurityContext.Create<SecurityContextProviderServer>();
                    if (SecurityContext.Current.Authenticate(token, tokenType))
                    {
                        return await SecurityContext.Current.ExecuteAsync(() => CreateAuthSuccessResponse(SecurityContext.Current));
                    }
                }
            }

            return await SecurityContext.Current.ExecuteAsync(() => CreateAuthFailedResponse());
        }

        private HttpResponseMessage CreateAuthSuccessResponse(SecurityContext current)
        {
            return new HttpResponseMessage(System.Net.HttpStatusCode.OK)
            {
                Content = new StringContent(JsonConvert.SerializeObject(new
                {
                    AuthenticationType = ((SuffuzIdentity)current.CurrentPrincipal.Identity).AuthenticationType,
                    BearerToken = ((SuffuzIdentity)current.CurrentPrincipal.Identity).BearerToken,
                    CustomerId = ((SuffuzIdentity)current.CurrentPrincipal.Identity).CustomerId,
                    IsAuthenticated = ((SuffuzIdentity)current.CurrentPrincipal.Identity).IsAuthenticated,
                    Name = ((SuffuzIdentity)current.CurrentPrincipal.Identity).Name,
                    Claims = ((SuffuzIdentity)current.CurrentPrincipal.Identity).Claims.Select(c => new SerializableClaim
                    {
                        Issuer = c.Issuer,
                        OriginalIssuer = c.OriginalIssuer,
                        Type = c.ClaimType,
                        Value = c.Value,
                        ValueType = c.ValueType
                    }).ToArray()
                }))
            };
        }

        private HttpResponseMessage CreateAuthFailedResponse()
        {
            return new HttpResponseMessage(System.Net.HttpStatusCode.Unauthorized);
        }
    }
}

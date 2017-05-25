using Microsoft.Owin;
using Microsoft.Owin.Security.Infrastructure;
using Microsoft.Owin.Security.OAuth;
using Microsoft.AspNet.Identity.Owin;
using Newtonsoft.Json;
using Common.Extensions;
using Common.OWIN;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Net;
using System.Runtime.Caching;
using System.Runtime.Remoting.Messaging;
using System.Security;
using System.Security.Principal;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using Microsoft.Owin.Security;

namespace Common.Security
{
    public class SHSServerTokenAuthenticator : IHttpTokenAuthenticator
    {
        static MemoryCache _cache = MemoryCache.Default;

        public bool Authenticate(string token, TokenType tokenType, out IPrincipal principal)
        {
            if (tokenType == TokenType.OAuth_SHS)
            {
                var hash = token.ToBase64SHA1();
                lock (_cache)
                {
                    principal = _cache[hash] as IPrincipal;
                }
                if (principal == null)
                {
                    var owin = (IOwinContext)CallContext.LogicalGetData("IOwinContext");
                    var oAuthOptions = owin.Get<OAuthBearerOptionsProvider>();
                    var ticket = oAuthOptions.Options.AccessTokenFormat.Unprotect(token.Replace("Bearer ", ""));

                    if (ValidateTicket(ticket))
                    {
                        var identityProvider = AppContext.Current.Container.GetInstance<IIdentityProvider>();
                        var identity = identityProvider.Create(ticket.Identity, ticket.Identity.FindFirst(c => c.Type.Equals("customer_id"))?.Value, ticket.Identity.FindFirst(c => c.Type.Equals("customer_name"))?.Value);
                        if (identity != null)
                        {
                            principal = new SHSPrincipal(identity);
                            if (identity is SHSIdentity)
                            {
                                ((SHSIdentity)identity).BearerToken = token;
                            }
                            foreach (var claim in ticket.Identity.Claims)
                            {
                                identity.Claims.Add(new Microsoft.IdentityModel.Claims.Claim(claim.Type, claim.Value, claim.ValueType, claim.Issuer, claim.OriginalIssuer));
                            }

                            lock (_cache)
                            {
                                _cache.Add(
                                    hash,
                                    principal,
                                    new CacheItemPolicy()
                                    {
                                        AbsoluteExpiration = ticket.Properties.ExpiresUtc.Value.ToLocalTime()
                                    });
                            }

                            return true;
                        }
                    }

                    principal = null;
                    return false;
                }
                else
                {
                    return true;
                }
            }
            else
            {
                throw new NotSupportedException("The token authentication type is not supported");
            }
        }

        private bool ValidateTicket(AuthenticationTicket ticket)
        {
            if (ticket != null && ticket.Properties.ExpiresUtc >= DateTime.UtcNow)
            {
                return true;
            }
            return false;
        }

        //public async Task<TokenResponse> Login(string username, string password)
        //{
        //    var request = HttpWebRequest.CreateHttp(AppContext.ApiUris.TokenUri);
        //    request.Method = "POST";
        //    request.Headers.Add("Origin", ConfigurationManager.AppSettings["BaseUri"]);

        //    string postString = String.Format("username={0}&password={1}&grant_type=password&client_id={2}&client_secret={3}&verbose=true",
        //        HttpUtility.HtmlEncode(username), 
        //        HttpUtility.HtmlEncode(password),
        //        HttpUtility.HtmlEncode(ConfigurationManager.AppSettings["oAuthClientId"]),
        //        HttpUtility.HtmlEncode(ConfigurationManager.AppSettings["oAuthClientSecret"]));

        //    byte[] bytes = Encoding.UTF8.GetBytes(postString);

        //    using (var requestStream = await request.GetRequestStreamAsync())
        //    {
        //        requestStream.Write(bytes, 0, bytes.Length);
        //    }

        //    try
        //    {
        //        HttpWebResponse httpResponse = (HttpWebResponse)(await request.GetResponseAsync());
        //        string json;
        //        using (var responseStream = httpResponse.GetResponseStream())
        //        {
        //            json = new StreamReader(responseStream).ReadToEnd();
        //        }
        //        var tokenResponse = JsonConvert.DeserializeObject<TokenResponse>(json);
        //        tokenResponse.Claims = JsonConvert.DeserializeObject<SerializableClaim[]>(tokenResponse.ClaimsText);
        //        return tokenResponse;
        //    }
        //    catch (Exception ex)
        //    {
        //        throw new SecurityException("Bad credentials", ex);
        //    }
        //}
    }

    
}

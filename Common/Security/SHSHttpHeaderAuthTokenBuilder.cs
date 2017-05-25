using Microsoft.IdentityModel.Claims;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace Common.Security
{
    public enum TokenType
    {
        OAuth_SHS,
        OAuth_ADFS,
        OAuth_Google,
        OAuth_Facebook,
        OAuth_Microsoft,
        LocalStore,
        Unknown
    }
    public class SHSHttpHeaderAuthTokenBuilder : IHttpHeaderAuthTokenBuilder
    {
        public const string SHS_AUTH_TOKEN_HEADER = "Authorization";
        public const string SHS_AUTH_TOKEN_USER = "username";
        public const string SHS_AUTH_TOKEN_PASSWORD = "password";
        public const string SHS_AUTH_TOKEN_CUSTOMER = "customer";
        public const string SHS_AUTH_TOKEN_VERBOSE = "verbose";
        public const string SHS_AUTH_TOKEN_CLIENT_ID = "client_id";
        public const string SHS_AUTH_TOKEN_CLIENT_SECRET = "client_secret";
        public const string SHS_AUTH_TOKEN_GRANT_TYPE = "grant_type";
        public const string SHS_OAUTH_BEARER = "Bearer";
        public const string SHS_AUTH = "OAuth_SHS";
        public const string SHS_OAUTH_BASIC = "Basic";

        public SHSHttpHeaderAuthTokenBuilder()
        {
        }

        public string GetRequestToken(out string headerName)
        {
            var identity = SecurityContext.Global.Provider.CurrentPrincipal != null && SecurityContext.Global.Provider.CurrentPrincipal.Identity != null
                ? (SHSIdentity)SecurityContext.Global.Provider.CurrentPrincipal.Identity
                : new SHSIdentity("", "", false);
            headerName = SHS_AUTH_TOKEN_HEADER;
            string token;
            DateTime expires;
            IClaimsIdentity tokenIdentity;
            if (identity.IsAuthenticated && SecurityContext.Global.Provider.TokenStore.TryGetToken(identity.Name, TokenType.LocalStore, out token, out expires, out tokenIdentity))
            {
                return string.Format(SHS_OAUTH_BEARER + " {0}", token);
            }
            else
            {
                return string.Format("{0} {1}",
                    SHS_OAUTH_BASIC,
                    EncodeBasicToken());
            }
        }

        private string EncodeBasicToken()
        {
            var plain = ConfigurationManager.AppSettings["client_id"] + ":" + ConfigurationManager.AppSettings["client_secret"];
            return Convert.ToBase64String(UTF8Encoding.UTF8.GetBytes(plain));
        }

        public bool ReadHeader(HttpRequestMessage httpRequest, out string token, out TokenType tokenType)
        {
            token = null;
            tokenType = TokenType.Unknown;
            if (httpRequest.Headers.Contains(SHS_AUTH_TOKEN_HEADER))
            {
                token = httpRequest.Headers.First(h => h.Key.Equals(SHS_AUTH_TOKEN_HEADER, StringComparison.InvariantCultureIgnoreCase)).Value.First();
                var split = token.Split(' ');
                if (split[0].ToLower() == SHS_OAUTH_BEARER.ToLower())
                {
                    tokenType = TokenType.OAuth_SHS;
                }
                else return false;
                return true;
            }
            else return false;
        }
    }
}

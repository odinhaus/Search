using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Principal;
using System.Text;
using System.Threading.Tasks;

namespace Common.Security
{
    public class SHSClientTokenAuthenticator : IHttpTokenAuthenticator
    {
        public bool Authenticate(string token, TokenType tokenType, out IPrincipal principal)
        {
            // TODO: make this call the web api when its running
            var authElements = token.Replace(SHSHttpHeaderAuthTokenBuilder.SHS_AUTH_TOKEN_HEADER, "").Split(',');
            string username = null, password = null, customerId = null;

            foreach (var element in authElements)
            {
                var trimmed = element.Trim();
                if (trimmed.StartsWith(SHSHttpHeaderAuthTokenBuilder.SHS_AUTH_TOKEN_USER, StringComparison.InvariantCultureIgnoreCase))
                {
                    username = trimmed.Split('=')[1].Replace("'", "").Replace("\"", "");
                }
                else if (trimmed.StartsWith(SHSHttpHeaderAuthTokenBuilder.SHS_AUTH_TOKEN_USER, StringComparison.InvariantCultureIgnoreCase))
                {
                    password = trimmed.Split('=')[1].Replace("'", "").Replace("\"", "");
                }
                else if (trimmed.StartsWith(SHSHttpHeaderAuthTokenBuilder.SHS_AUTH_TOKEN_CUSTOMER, StringComparison.InvariantCultureIgnoreCase))
                {
                    customerId = trimmed.Split('=')[1].Replace("'", "").Replace("\"", "");
                }
            }

            principal = new SHSPrincipal(new SHSIdentity(username, customerId, true));
            return true;
        }
    }
}

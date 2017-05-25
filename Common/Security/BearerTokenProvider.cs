using Microsoft.Owin.Security.OAuth;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Common.Security
{
    public class BearerTokenProvider : OAuthBearerAuthenticationProvider
    {
        public override Task RequestToken(OAuthRequestTokenContext context)
        {
            var value = context.Request.Headers["Authorization"];

            if (!string.IsNullOrEmpty(value))
            {
                context.Token = value.Replace("Bearer ", "");
            }

            return Task.FromResult<object>(null);
        }
    }
}

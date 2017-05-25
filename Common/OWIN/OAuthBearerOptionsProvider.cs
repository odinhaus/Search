using Microsoft.Owin.Security.OAuth;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Common.OWIN
{
    public class OAuthBearerOptionsProvider : IDisposable
    {
        public OAuthBearerOptionsProvider(OAuthBearerAuthenticationOptions options)
        {
            Options = options;
        }
        public OAuthBearerAuthenticationOptions Options { get; private set; }
        public void Dispose()
        {
            
        }
    }
}

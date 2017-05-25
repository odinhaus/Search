using Microsoft.IdentityModel.Claims;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Principal;
using System.Text;
using System.Threading.Tasks;

namespace Common.Security
{
    public class SecurityContextProviderServer : SecurityContextProviderClient
    {
        public SecurityContextProviderServer(IHttpTokenAuthenticator tokenAuthenticator) : base(tokenAuthenticator)
        {
        }

        public override IPrincipal Authenticate(string token, TokenType tokenType)
        {
            IPrincipal principal;
            var result = TokenAuthenticator.Authenticate(token, tokenType, out principal);
            CurrentPrincipal = principal;
            if (result && CurrentImpersonationPrincipal != null)
            {
                Impersonate(CurrentImpersonationPrincipal.Identity.Name);
            }
            OnAuthenticationComplete();
            return CurrentPrincipal;
        }

        protected override string OnGetDeviceId()
        {
            if (CurrentPrincipal.Identity.IsAuthenticated)
            {
                return ((IClaimsIdentity)CurrentPrincipal.Identity).Claims.FirstOrDefault(c => c.ClaimType.Equals(ClaimTypesEx.DeviceId))?.Value ?? null;
            }
            else
            {
                return null;
            }
        }

        public override bool ChangePassword(string currentPassword, string newPassword, string confirmPassword, out string message)
        {
            throw new NotSupportedException();
        }
    }
}

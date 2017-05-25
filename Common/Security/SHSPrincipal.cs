using Microsoft.IdentityModel.Claims;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Principal;
using System.Text;
using System.Threading.Tasks;

namespace Common.Security
{
    public class SHSPrincipal : IClaimsPrincipal
    {
        public SHSPrincipal(IClaimsIdentity identity)
        {
            Identity = identity;
            Identities = new ClaimsIdentityCollection(new IClaimsIdentity[] { identity });
        }

        public ClaimsIdentityCollection Identities
        {
            get;
            private set;
        }

        public IIdentity Identity
        {
            get;
            private set;
        }

        public IClaimsPrincipal Copy()
        {
           return new SHSPrincipal(((IClaimsIdentity)this.Identity).Copy());
        }

        public bool IsInRole(string role)
        {
            return Identities.Where(i => i.RoleClaimType == ClaimTypes.Role)
                .Any(i => i.Claims.Any(c => c.Value.Equals(role, StringComparison.CurrentCultureIgnoreCase)));
        }
    }
}

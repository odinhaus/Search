using Microsoft.IdentityModel.Claims;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Data.Core.Security
{
    public static class SHSPrincipalEx
    {
        public static IEnumerable<IOrgUnit> GetOrgUnits(this IClaimsPrincipal principal)
        {
            if (principal.Identity?.IsAuthenticated ?? false)
            {
                IOrgUnit ou = null;
                for (int i = 0; i < ((IClaimsIdentity)principal.Identity).Claims.Count; i++)
                {
                    var claim = ((IClaimsIdentity)principal.Identity).Claims[i];
                    if (claim.ClaimType == "OrgUnit" && claim.OriginalIssuer == "NNHIS")
                    {
                        ou = Model.New<IOrgUnit>();
                        ou.Name = claim.Value;
                    }
                    else if (claim.ClaimType == "OrgUnit_Id" && claim.OriginalIssuer == "NNHIS")
                    {
                        ou.SetKey(claim.Value);
                        yield return ou;
                    }
                }
            }
        }
    }
}

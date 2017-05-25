using Microsoft.IdentityModel.Claims;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Principal;
using System.Text;
using System.Threading.Tasks;

namespace Common.Security
{
    public interface IIdentityProvider
    {
        IClaimsIdentity Create(IIdentity identity, string customer_id, string customer_prefix);
    }
}

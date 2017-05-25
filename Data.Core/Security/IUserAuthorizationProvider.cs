using Common.Security;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Data.Core.Security
{
    public interface IUserAuthorizationProvider
    {
        SerializableClaim[] GetClaims();
    }
}

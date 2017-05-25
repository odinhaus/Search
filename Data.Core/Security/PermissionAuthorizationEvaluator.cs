using Common.Security;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Data.Core.Security
{
    public class PermissionAuthorizationEvaluator : ICustomAuthorizationEvaluator
    {
        public PermissionAuthorizationEvaluator(IEnumerable<string> permissions)
        {
            this.Permissions = permissions;
        }

        public IEnumerable<string> Permissions { get; private set; }

        public bool Demand()
        {
            return Permission.TryDemand(Permissions);
        }
    }
}

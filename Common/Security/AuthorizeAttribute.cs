using System;
using System.Collections.Generic;
using System.Linq;
using System.Security;
using System.Security.Permissions;
using System.Text;
using System.Threading.Tasks;

namespace Common.Security
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true, Inherited = true)]
    public class AuthorizeAttribute : CodeAccessSecurityAttribute
    {
        public AuthorizeAttribute(SecurityAction action) : base(action)
        {
        }

        public string Role { get; set; }
        public string User { get; set; }

        public bool IsAlwaysDenied { get; set; }

        public override IPermission CreatePermission()
        {
            var perm = new AuthorizePermission();
            if (!string.IsNullOrEmpty(Role))
            {
                perm.Roles = new string[] { Role };
            }
            if (!string.IsNullOrEmpty(User))
            {
                perm.Users = new string[] { User };
            }
            perm.IsAlwaysDenied = this.IsAlwaysDenied;
            return perm;
        }
    }
}

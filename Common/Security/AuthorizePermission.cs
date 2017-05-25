using Microsoft.IdentityModel.Claims;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security;
using System.Text;
using System.Threading.Tasks;

namespace Common.Security
{
    public class AuthorizePermission : IPermission
    {
        public AuthorizePermission()
        {
            this.Roles = new string[0];
            this.Users = new string[0];
        }

        public string[] Roles { get; set; }
        public string[] Users { get; set; }
        public bool IsAlwaysDenied { get; set; }
        public IPermission Copy()
        {
            var perm = new AuthorizePermission()
            {
                Roles = this.Roles,
                Users = this.Users
            };
            return perm;
        }

        public void Demand()
        {
            if (this.IsAlwaysDenied)
                goto failed;

            if ((System.Threading.Thread.CurrentPrincipal?.Identity?.IsAuthenticated ?? false))
            {
                if (this.Users.Length > 0 && !(this.Users.Any(u => u.Equals(SecurityContext.Current.CurrentPrincipal.Identity.Name))))
                {
                    if (Roles.Length == 0)
                        goto failed;
                }
                if (this.Roles.Length > 0)
                {
                    var identity = SecurityContext.Current.CurrentPrincipal.Identity as IClaimsIdentity;
                    if (!(identity != null
                                && this.Roles.Any(r => identity.Claims.Any(c => c.ClaimType.Equals(ClaimTypes.Role)
                                    && c.Value.Equals(r, StringComparison.InvariantCultureIgnoreCase)))))
                    {
                        goto failed;
                    }
                }
                return;
            }
            
        failed:
            throw new SecurityException("The current principal is not authorized to perform this action");
        }


        public IPermission Union(IPermission target)
        {
            if (!(target is AuthorizePermission))
                throw new InvalidOperationException("Target permission must be of type AuthorizePermission");

            var roles = this.Roles.Union(((AuthorizePermission)target).Roles).ToArray();
            var users = this.Users.Union(((AuthorizePermission)target).Users).ToArray();
            var permission = new AuthorizePermission()
            {
                Roles = roles,
                Users = users
            };
            return permission;
        }

        public IPermission Intersect(IPermission target)
        {
            if (!(target is AuthorizePermission))
                throw new InvalidOperationException("Target permission must be of type AuthorizePermission");

            var roles = this.Roles.SelectMany(r => ((AuthorizePermission)target).Roles.Where(tr => tr.Equals(r))).ToArray();
            var users = this.Users.SelectMany(u => ((AuthorizePermission)target).Users.Where(tu => tu.Equals(u))).ToArray();
            var permission = new AuthorizePermission()
            {
                Roles = roles,
                Users = users
            };
            return permission;
        }

        public bool IsSubsetOf(IPermission target)
        {
            if (!(target is AuthorizePermission)) return false;
            return this.Roles.All(r => ((AuthorizePermission)target).Roles.Any(rt => rt.Equals(r)));
        }

        public void FromXml(SecurityElement e)
        {
            this.Roles =  e.Attribute("Roles").Split(',');
            this.Users = e.Attribute("Users").Split(',');
        }

        public SecurityElement ToXml()
        {
            var element = new SecurityElement("IPermission");
            Type type = this.GetType();
            StringBuilder AssemblyName = new StringBuilder(type.Assembly.ToString());
            AssemblyName.Replace('\"', '\'');
            element.AddAttribute("class", type.FullName + ", " + AssemblyName);
            element.AddAttribute("version", "1");
            element.AddAttribute("Roles", this.Roles.Aggregate((r1, r2) => r1 + "," + r2 ));
            element.AddAttribute("Users", this.Users.Aggregate((u1, u2) => u1 + ',' + u2 ));
            return element;
        }

        
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Caching;
using System.Security;
using System.Text;
using System.Threading.Tasks;

namespace Common.Security
{
    public class Permission : IPermission
    {
        static MemoryCache _cache = new MemoryCache("Permissions_Cache");

        public Permission()
        {
            Names = new string[0];
        }

        public Permission(string permissionName) : this(new string[] { permissionName })
        { }

        public Permission(IEnumerable<Permission> permissions) : this(permissions.SelectMany(p => p.Names))
        { }

        public Permission(IEnumerable<string> permissionNames)
        {
            if (permissionNames == null)
            {
                this.Names = new string[0];
            }
            else
            {
                this.Names = permissionNames.ToArray();
            }
        }

        public string[] Names { get; private set; }

        public IPermission Copy()
        {
            return new Permission(Names);
        }

        public static bool TryDemand(IEnumerable<string> permissions)
        {
            if (!(SecurityContext.Current?.CurrentPrincipal?.Identity.IsAuthenticated ?? false))
            {
                return false;
            }
            try
            {
                var permission = new Permission(permissions);
                permission.Demand();
                return true;
            }
            catch
            {
                return false;
            }
        }

        public void Demand()
        {
            Permission permission;

            lock (_cache)
            {
                permission = _cache[SecurityContext.Current.CurrentPrincipal.Identity.Name] as Permission;
                if (permission == null)
                {
                    var provider = AppContext.Current.Container.GetInstance<IPermissionProvider>();
                    var permissions = provider.GetPermissions();
                    permission = new Permission();
                    foreach (var p in permissions)
                    {
                        permission = (Permission)permission.Union(p);
                    }
                    _cache.Add(SecurityContext.Current.CurrentPrincipal.Identity.Name,
                        permission,
                        new CacheItemPolicy() { SlidingExpiration = TimeSpan.FromMinutes(30) });
                }

                if (!IsSubsetOf(permission))
                    throw new SecurityException("Access is denied.");
            }
        }

        public IPermission Intersect(IPermission target)
        {
            if (target is Permission)
            {
                return new Permission(((Permission)target).Names.Intersect(this.Names));
            }
            else
                throw new InvalidOperationException("Target permission must be of type Permission.");
        }

        public bool IsSubsetOf(IPermission target)
        {
            if (target is Permission)
            {
                return this.Names.All(n => ((Permission)target).Names.Any(nn => nn == n));
            }
            else
                throw new InvalidOperationException("Target permission must be of type Permission.");
        }

        public IPermission Union(IPermission target)
        {
            if (target is Permission)
            {
                return new Permission(Names.Union(((Permission)target).Names));
            }
            else
                throw new InvalidOperationException("Target permission must be of type Permission.");
        }

        public void FromXml(SecurityElement e)
        {
            throw new NotImplementedException();
        }

        public SecurityElement ToXml()
        {
            throw new NotImplementedException();
        }

        public static implicit operator Permission(string permission)
        {
            return new Permission(permission);
        }

        public static implicit operator Permission(string[] permissions)
        {
            return new Permission(permissions);
        }
    }
}

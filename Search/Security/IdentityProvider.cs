using Common;
using Common.Security;
using Data;
using Data.Core;
using Data.Core.Linq;
using Data.Core.Security;
using Microsoft.IdentityModel.Claims;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Principal;
using System.Text;
using System.Threading.Tasks;

namespace Search.Security
{
    public class IdentityProvider : IIdentityProvider, IUserAuthorizationProvider
    {
        public IClaimsIdentity Create(IIdentity identity, string customer_id, string customer_name)
        {
            var currentPrincipal = SecurityContext.Current.CurrentPrincipal;
            try
            {
                SecurityContext.Current.CurrentPrincipal = new SuffuzPrincipal(SuffuzIdentity.Admin);
                var queryProvider = AppContext.Current.Container.GetInstance<IModelQueryProviderBuilder>().CreateQueryProvider<IAny>();
                var query = string.Format("{0}{{Username = '{3}'}}+>{1}+>{2} RETURNS PATHS",
                    ModelTypeManager.GetModelName<IUser>(),
                    ModelTypeManager.GetModelName<isMemberOf>(),
                    ModelTypeManager.GetModelName<IRole>(),
                    identity.Name);
                var results = queryProvider.Query(query);
                IUser user = null;
                if (results == null || results.Count == 0)
                {
                    var ppUser = AppContext.Current.Container.GetInstance<IModelPersistenceProviderBuilder>().CreatePersistenceProvider<IUser>();
                    var ppIsInrole = AppContext.Current.Container.GetInstance<IModelPersistenceProviderBuilder>().CreatePersistenceProvider<isMemberOf>();
                    user = Model.New<IUser>();
                    user.Username = identity.Name;
                    user.Icon = Convert.FromBase64String(IUserDefaults.DefaultIcon);
                    user.Title = IUserDefaults.UserTitle;
                    var role = IRoleDefaults.UsersRole;
                    var isInRole = Model.New<isMemberOf>();
                    isInRole.To = role;
                    user = ppUser.Create(user, IOrgUnitDefaults.RootOrgUnit);
                    isInRole.From = user;
                    ppIsInrole.Create(isInRole, IOrgUnitDefaults.RootOrgUnit);
                    results = queryProvider.Query(query);
                    user = null;
                }

                var orgUnitInitializer = AppContext.Current.Container.GetInstance<IOrgUnitInitializer>();
                var orgUnit = orgUnitInitializer.Create(customer_id, customer_name);

                query = string.Format("{0}{{Username = '{3}'}}->{1}->{2}{{Name = '{4}'}}",
                    ModelTypeManager.GetModelName<IUser>(),
                    ModelTypeManager.GetModelName<isMemberOf>(),
                    ModelTypeManager.GetModelName<IOrgUnit>(),
                    identity.Name,
                    customer_id
                    );

                user = queryProvider.Query(query).OfType<IUser>().FirstOrDefault();

                if (user == null)
                {
                    query = string.Format("{0}{{Username = '{1}'}}",
                        ModelTypeManager.GetModelName<IUser>(),
                        identity.Name
                        );
                    user = queryProvider.Query(query).OfType<IUser>().FirstOrDefault();
                    var ppMember = AppContext.Current.Container.GetInstance<IModelPersistenceProviderBuilder>().CreatePersistenceProvider<isMemberOf>();
                    var imo = Model.New<isMemberOf>();
                    imo.From = user;
                    imo.To = orgUnit;
                    ppMember.Create(imo, orgUnit);
                }

                user = null;

                var roles = new List<IRole>();
                foreach (var path in results.OfType<Path<IAny>>())
                {
                    if (user == null)
                        user = (IUser)path.Root;
                    if (path.Nodes.Length > 1)
                    {
                        roles.Add((IRole)path.Nodes[1]);
                    }
                }

                var claimsIdentity = new SuffuzIdentity(user.Username, customer_id, true);
                foreach (var role in roles)
                {
                    if (role == null) continue;
                    claimsIdentity.Claims.Add(new Claim(ClaimTypes.Role, role.Name, ClaimValueTypes.String, AppContext.Name, AppContext.Name));
                }

                query = string.Format("{0}<-{1}<-{2}{{Username = '{3}'}}",
                    ModelTypeManager.GetModelName<IOrgUnit>(),
                    ModelTypeManager.GetModelName<isMemberOf>(),
                    ModelTypeManager.GetModelName<IUser>(),
                    identity.Name
                    );
                foreach (var ou in queryProvider.Query(query).OfType<IOrgUnit>())
                {
                    claimsIdentity.Claims.Add(new Claim("OrgUnit", ou.Name, ClaimValueTypes.String, AppContext.Name, AppContext.Name));
                }

                return claimsIdentity;
            }
            finally
            {
                SecurityContext.Current.CurrentPrincipal = currentPrincipal;
            }
        }

        public SerializableClaim[] GetClaims()
        {
            if (SecurityContext.Current?.CurrentPrincipal?.Identity?.IsAuthenticated ?? false)
            {
                var queryProvider = AppContext.Current.Container.GetInstance<IModelQueryProviderBuilder>().CreateQueryProvider<IAny>();
                var identity = SecurityContext.Current.CurrentPrincipal.Identity as SuffuzIdentity;
                var results = queryProvider.Query(string.Format("{1}{{Username = '{0}'}}+>{2}+>{3} RETURNS PATHS",
                    identity.Name,
                    ModelTypeManager.GetModelName<IUser>(),
                    ModelTypeManager.GetModelName<isMemberOf>(),
                    ModelTypeManager.GetModelName<IRole>()));

                var claims = new List<SerializableClaim>();

                if (results != null && results.Count > 0)
                {
                    foreach (var path in results.OfType<Path<IAny>>())
                    {
                        if (path.Nodes.Length > 1 && path.Nodes[1] != null)
                        {
                            if (!claims.Any(c => c.Type.Equals(ClaimTypes.Role) && c.Value.Equals(((IRole)path.Nodes[1]).Name) && c.Issuer.Equals(AppContext.Name)))
                            {
                                claims.Add(new SerializableClaim()
                                {
                                    Type = ClaimTypes.Role,
                                    Value = ((IRole)path.Nodes[1]).Name,
                                    ValueType = ClaimValueTypes.String,
                                    Issuer = AppContext.Name,
                                    OriginalIssuer = AppContext.Name
                                });
                            }
                        }
                    }
                }

                var query = string.Format("{0}<-{1}<-{2}{{Username = '{3}'}}",
                    ModelTypeManager.GetModelName<IOrgUnit>(),
                    ModelTypeManager.GetModelName<isMemberOf>(),
                    ModelTypeManager.GetModelName<IUser>(),
                    identity.Name
                    );
                foreach (var ou in queryProvider.Query(query).OfType<IOrgUnit>())
                {
                    claims.Add(new SerializableClaim()
                    {
                        Type = "OrgUnit",
                        Value = ou.Name,
                        ValueType = ClaimValueTypes.String,
                        Issuer = AppContext.Name,
                        OriginalIssuer = AppContext.Name
                    });
                    claims.Add(new SerializableClaim()
                    {
                        Type = "OrgUnit_Id",
                        Value = ou.Key.ToString(),
                        ValueType = ClaimValueTypes.String,
                        Issuer = AppContext.Name,
                        OriginalIssuer = AppContext.Name
                    });
                    claims.Add(new SerializableClaim()
                    {
                        Type = "OrgUnit_Prefix",
                        Value = ou.Prefix,
                        ValueType = ClaimValueTypes.String,
                        Issuer = AppContext.Name,
                        OriginalIssuer = AppContext.Name
                    });
                }

                return claims.ToArray();
            }
            else
            {
                return new SerializableClaim[0];
            }
        }
    }
}

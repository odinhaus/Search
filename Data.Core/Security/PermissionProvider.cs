using Common.Security;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Security;
using Common;
using Microsoft.IdentityModel.Claims;

namespace Data.Core.Security
{
    public class PermissionProvider : IPermissionProvider
    {
        public IEnumerable<IPermission> GetPermissions()
        {
            if ((Common.Security.SecurityContext.Current?.CurrentPrincipal?.Identity.IsAuthenticated ?? false))
            {
                var qp = AppContext.Current.Container.GetInstance<IModelQueryProviderBuilder>().CreateQueryProvider<IPerm>();
                var query = string.Format("{0}<-{1}<-{2}<-{3}<-{4}{{Username='{5}'}}",
                                ModelTypeManager.GetModelName<IPerm>(),
                                ModelTypeManager.GetModelName<hasPermission>(),
                                ModelTypeManager.GetModelName<IRole>(),
                                ModelTypeManager.GetModelName<isMemberOf>(),
                                ModelTypeManager.GetModelName<IUser>(),
                                Common.Security.SecurityContext.Current.CurrentPrincipal.Identity.Name);
                var perms = qp.Query(query).Cast<IPerm>();
                foreach (var perm in perms)
                    yield return new Permission(perm.Name);
            }
        }
    }
}

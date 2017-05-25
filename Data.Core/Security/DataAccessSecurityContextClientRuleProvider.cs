using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Data.Core.Linq;
using Common;
using Microsoft.IdentityModel.Claims;
using Common.Security;

namespace Data.Core.Security
{
    public class DataAccessSecurityContextClientRuleProvider : IDataAccessSecurityContextRuleProvider
    {
        public IEnumerable<IPath<IOrgUnit>> GetRules()
        {
            var rqp = AppContext.Current.Container.GetInstance<IModelQueryProviderBuilder>().CreateQueryProvider<IRule>();
            var paths = rqp.Query(string.Format("{0}{{Key = {3} or Name = '{4}'}}->{1}->{2} RETURNS PATHS",
               ModelTypeManager.GetModelName<IOrgUnit>(),
               ModelTypeManager.GetModelName<owns>(),
               ModelTypeManager.GetModelName<IRule>(),
               GetOrgUnitKey(),
               IOrgUnitDefaults.RootOrgUnitName));

            foreach (var path in paths.OfType<Path<IAny>>())
                yield return new Path<IOrgUnit>()
                {
                    Root = path.Root as IOrgUnit,
                    Nodes = path.Nodes,
                    Edges = path.Edges
                };
        }

        private string GetOrgUnitKey()
        {
            var ci = SecurityContext.Current.CurrentPrincipal.Identity as IClaimsIdentity;
            return ci.Claims.Single(c => c.ClaimType == "OrgUnit_Id" && c.OriginalIssuer == "NNHIS").Value;
        }
    }
}

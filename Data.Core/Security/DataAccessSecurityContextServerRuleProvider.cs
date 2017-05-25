using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Data.Core.Linq;
using Common;

namespace Data.Core.Security
{
    public class DataAccessSecurityContextServerRuleProvider : IDataAccessSecurityContextRuleProvider
    {
        public IEnumerable<IPath<IOrgUnit>> GetRules()
        {
            var rqp = AppContext.Current.Container.GetInstance<IModelQueryProviderBuilder>().CreateQueryProvider<IRule>();
            var paths = rqp.Query(string.Format("{0}->{1}->{2}{{TargetApp = '{3}' or TargetApp = '{4}'}} RETURNS PATHS",
               ModelTypeManager.GetModelName<IOrgUnit>(),
               ModelTypeManager.GetModelName<owns>(),
               ModelTypeManager.GetModelName<IRule>(),
               AppContext.Name,
               IOrgUnitDefaults.RootOrgUnitName));
            foreach (var path in paths.OfType<Path<IAny>>())
                yield return new Path<IOrgUnit>()
                {
                    Root = path.Root as IOrgUnit,
                    Nodes = path.Nodes,
                    Edges = path.Edges
                };
        }
    }
}

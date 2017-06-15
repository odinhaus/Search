using Common.Security;
using Data;
using Data.Core;
using Data.Core.Security;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Suffuz.Data
{
    public class OrgUnitInitializer : DefaultOrgUnitInitializer
    {
        protected override IEnumerable<IRule> CreateSecurityRules()
        {
            var baseRules =  base.CreateSecurityRules();
            var rule = baseRules.First();
            var usersCanManageTheirAppToken = Model.New<IRulePolicy>();
            usersCanManageTheirAppToken.FailureMessage = "Rule 6: User can manage their own AppToken.";
            usersCanManageTheirAppToken.ModelTypes = new string[] { ModelTypeManager.GetModelName<IAppToken>() };
            usersCanManageTheirAppToken.PolicySelector = "'*'";
            usersCanManageTheirAppToken.Entitlement = Entitlement.Allow;
            usersCanManageTheirAppToken.Actions = DataActions.All;
            usersCanManageTheirAppToken.ModelEvaluator = "IsInRole(@user, '" + IRoleDefaults.AdministratorsRoleName + "') || @model.UserName == @user.Username";
            usersCanManageTheirAppToken.UserEvaluator = "IsAuthenticated(@user)";
            rule.Policies.Add(usersCanManageTheirAppToken);
            return baseRules;
        }
    }
}

using Common;
using Common.Security;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Data.Core.Security
{
    public class DefaultOrgUnitInitializer : IOrgUnitInitializer
    {
        static object _sync = new object();
        public IOrgUnit Create(string name, string prefix)
        {
            var sec = DataAccessSecurityContext.Current.IsEnabled;
            var principal = SecurityContext.Current.CurrentPrincipal;
            try
            {
                DataAccessSecurityContext.Current.IsEnabled = false;
                lock (_sync)
                {
                    var qp = AppContext.Current.Container.GetInstance<IModelQueryProviderBuilder>().CreateQueryProvider<IOrgUnit>();

                    var orgUnit = qp.Query(string.Format("{0}{{Name ='{1}'}}", ModelTypeManager.GetModelName<IOrgUnit>(), name)).OfType<IOrgUnit>().FirstOrDefault();
                    var mppb = AppContext.Current.Container.GetInstance<IModelPersistenceProviderBuilder>();
                    if (orgUnit == null)
                    {

                        var oumpp = mppb.CreatePersistenceProvider<IOrgUnit>();
                        orgUnit = Model.New<IOrgUnit>();
                        orgUnit.Name = name;
                        orgUnit.Prefix = prefix;

                        orgUnit = oumpp.Create(orgUnit, IOrgUnitDefaults.RootOrgUnit);
                    }

                    var hasRules = qp.Query(string.Format("{0}{{TargetApp = '{4}'}}<-{1}<-{2}{{Name = '{3}'}}",
                        ModelTypeManager.GetModelName<IRule>(),
                        ModelTypeManager.GetModelName<owns>(),
                        ModelTypeManager.GetModelName<IOrgUnit>(),
                        orgUnit.Name,
                        AppContext.Name)).Count > 0;

                    if (!hasRules)
                    {
                        var rules = CreateSecurityRules();

                        var srpp = mppb.CreatePersistenceProvider<IRule>();
                        foreach (var rule in rules)
                        {
                            rule.Name = orgUnit.Name + "." + rule.Name;
                            rule.Key = srpp.Create(rule, orgUnit).Key;
                            ((_IOwnedModel)rule).Owner = orgUnit;
                        }

                        var cmpp = mppb.CreatePersistenceProvider<IContainer>();
                        var container = Model.New<IContainer>();
                        container.Name = IContainerDefaults.DefaultContainer(orgUnit);
                        cmpp.Create(container, orgUnit);

                        foreach (var rule in rules)
                            DataAccessSecurityContext.Current.AddRule(rule);
                    }

                    return orgUnit;
                }
            }
            finally
            {
                DataAccessSecurityContext.Current.IsEnabled = sec;
            }
        }

        protected virtual IEnumerable<IRule> CreateSecurityRules()
        {
            var rule = Model.New<IRule>();
            rule.Description = "Users can create and manage data for their org unit";
            rule.Name = "SecurityRules";
            rule.EffectiveStartDate = DateTime.MinValue;
            rule.EffectiveEndDate = DateTime.MaxValue;
            rule.Rank = 0;
            rule.TargetApp = AppContext.Name;

            var read = Model.New<IRulePolicy>();
            read.FailureMessage = "Rule 0: The current user cannot read the requested data.";
            read.ModelTypes = new string[] { "*" };
            read.PolicySelector = "'*'";
            read.Entitlement = Entitlement.Allow;
            read.Actions = DataActions.Read;
            read.ModelEvaluator = "@model == null "
                                + "|| @user = @model "
                                + "|| @to = @user "
                                + "|| @from = @user "
                                + "|| @model.Owner == null "
                                + "|| IsMemberOf(@user, @model.Owner) "
                                + "|| (@modelClass == '" + ModelTypeManager.GetModelName<IOrgUnit>() + "' && IsMemberOf(@user, @model)) "
                                + "|| (@modelClass == '" + ModelTypeManager.GetModelName<IRole>() + "' && IsMemberOf(@user, @model)) "
                                + "|| (@modelClass == '" + ModelTypeManager.GetModelName<IRule>() + "' && IsOwnedBy(@model, '" + IOrgUnitDefaults.RootOrgUnitName + "')) "
                                + "|| (@modelClass == '" + ModelTypeManager.GetModelName<owns>() + "' && @from == @rootOrgUnit && @toClass == '" + ModelTypeManager.GetModelName<IRule>() + "')";
            read.UserEvaluator = "IsAuthenticated(@user)";
            rule.Policies.Add(read);

            var createAdmin = Model.New<IRulePolicy>();
            createAdmin.FailureMessage = "Rule 1: Global Admins can administer data.";
            createAdmin.ModelTypes = new string[] { "*" };
            createAdmin.PolicySelector = "'*'";
            createAdmin.Entitlement = Entitlement.Allow;
            createAdmin.Actions = DataActions.All;
            createAdmin.ModelEvaluator = "'*'";
            createAdmin.UserEvaluator = "IsAuthenticated(@user) && IsInRole(@user, '" + IRoleDefaults.AdministratorsRoleName + "')";
            rule.Policies.Add(createAdmin);


            var create = Model.New<IRulePolicy>();
            create.FailureMessage = "Rule 2: The current user cannot create the requested data.";
            create.ModelTypes = new string[] { "*" };
            create.PolicySelector = "@modelClass != '" + ModelTypeManager.GetModelName<IRule>()
                + "' && @modelClass != '" + ModelTypeManager.GetModelName<IOrgUnit>()
                + "' && @modelClass != '" + ModelTypeManager.GetModelName<IRole>()
                + "' && @modelClass != '" + ModelTypeManager.GetModelName<IUser>() + "'";
            create.Entitlement = Entitlement.Allow;
            create.Actions = DataActions.Create | DataActions.Update | DataActions.Link;
            create.ModelEvaluator = "@model == null || @model.IsNew "
                                  + "|| (IsMemberOf(@user, @model.Owner) && (@to == null || @to == @user || IsMemberOf(@user, @to.Owner)) && (@from == null || @from == @user || IsMemberOf(@user, @from.Owner))) "
                                  + "|| (@modelClass == '" + ModelTypeManager.GetModelName<owns>() + "' && IsMemberOf(@user, @from)) ";
            create.UserEvaluator = "IsAuthenticated(@user)";
            rule.Policies.Add(create);


            var delete = Model.New<IRulePolicy>();
            delete.FailureMessage = "Rule 3: The current user cannot delete the requested data.";
            delete.ModelTypes = new string[] { "*" };
            delete.PolicySelector = "'*'";
            delete.Entitlement = Entitlement.Allow;
            delete.Actions = DataActions.Delete;
            delete.ModelEvaluator = "IsMemberOf(@user, @model.Owner) || IsInRole(@user, '" + IRoleDefaults.AdministratorsRoleName + "')";
            delete.UserEvaluator = "IsAuthenticated(@user)";
            rule.Policies.Add(delete);


            var usersCanReadUsers = Model.New<IRulePolicy>();
            usersCanReadUsers.FailureMessage = "Rule 4: Users can read other users' information.";
            usersCanReadUsers.ModelTypes = new string[] { ModelTypeManager.GetModelName<IUser>() };
            usersCanReadUsers.PolicySelector = "'*'";
            usersCanReadUsers.Entitlement = Entitlement.Allow;
            usersCanReadUsers.Actions = DataActions.Read;
            usersCanReadUsers.ModelEvaluator = "'*'";
            usersCanReadUsers.UserEvaluator = "IsAuthenticated(@user)";
            rule.Policies.Add(usersCanReadUsers);


            var usersCanEditTheirProfile = Model.New<IRulePolicy>();
            usersCanEditTheirProfile.FailureMessage = "Rule 5: Users can update their information.";
            usersCanEditTheirProfile.ModelTypes = new string[] { ModelTypeManager.GetModelName<IUser>() };
            usersCanEditTheirProfile.PolicySelector = "'*'";
            usersCanEditTheirProfile.Entitlement = Entitlement.Allow;
            usersCanEditTheirProfile.Actions = DataActions.Write;
            usersCanEditTheirProfile.ModelEvaluator = "@user == @model || IsInRole(@user, '" + IRoleDefaults.AdministratorsRoleName + "')";
            usersCanEditTheirProfile.UserEvaluator = "IsAuthenticated(@user)";
            rule.Policies.Add(usersCanEditTheirProfile);


            var hasPermissionTest = Model.New<IRulePolicy>();
            hasPermissionTest.FailureMessage = "Rule 6: User has Test permission.";
            hasPermissionTest.ModelTypes = new string[] { "*" };
            hasPermissionTest.PolicySelector = "@customArg == 'CanRunQueries'";
            hasPermissionTest.Entitlement = Entitlement.Allow;
            hasPermissionTest.Actions = DataActions.Custom;
            hasPermissionTest.ModelEvaluator = "HasPermissions([@customArg])";
            hasPermissionTest.UserEvaluator = "IsAuthenticated(@user)";
            rule.Policies.Add(hasPermissionTest);

           
            return new IRule[] { rule };
        }
    }
}

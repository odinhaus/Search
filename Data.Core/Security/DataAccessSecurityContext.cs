using Common;
using Common.Diagnostics;
using Common.Security;
using Data.Core.Auditing;
using Data.Core.Linq;
using Data.Core.Evaluation;
using Data.Core.Security;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Microsoft.IdentityModel.Claims;

namespace Data.Core.Security
{
    public enum EnforcementType
    {
        Optimistic = 0,
        Pessimistic = 1
    }

    public class DataAccessSecurityContext
    {
        static Dictionary<string, OrgUnitDataAccessSecurity> _rules = new Dictionary<string, OrgUnitDataAccessSecurity>();
        [ThreadStatic]
        static DataAccessSecurityContext _ctx = new DataAccessSecurityContext();


        private DataAccessSecurityContext()
        {
            IsEnabled = true;
        }

        public static void Initialize()
        {
            lock (_rules)
            {
                if (IsInitialized)
                {
                    return;
                }
            }
            //var rqp = AppContext.Current.Container.GetInstance<IModelQueryProviderBuilder>().CreateQueryProvider<IRule>();
            //var paths = rqp.Query(string.Format("{0}->{1}->{2} RETURNS PATHS",
            //    ModelTypeManager.GetModelName<IOrgUnit>(),
            //    ModelTypeManager.GetModelName<owns>(),
            //    ModelTypeManager.GetModelName<IRule>()));
            var paths = AppContext.Current.Container.GetInstance<IDataAccessSecurityContextRuleProvider>().GetRules();
            var compiler = AppContext.Current.Container.GetInstance<IRuleCompiler>();

            foreach (var path in paths)
            {
                OrgUnitDataAccessSecurity oudas = null;

                try
                {
                    oudas = _rules[path.Root.GetKey()];
                }
                catch
                {
                    oudas = new OrgUnitDataAccessSecurity(path.Root.GetKey(), compiler);
                    _rules.Add(path.Root.GetKey(), oudas);
                }

                oudas.AddRule(path.Nodes[1] as IRule);
            }

            IsInitialized = true;
        }

        public static DataAccessSecurityContext Current
        {
            get
            {
                if (_ctx == null)
                {
                    _ctx = new DataAccessSecurityContext();
                }
                return _ctx;
            }
        }

        public bool IsEnabled { get; set; }
        public static bool IsInitialized { get; private set; }
        public static EnforcementType EnforcementType { get; set; }

        public void AddRule(IRule rule)
        {
            lock (_rules)
            {
                if (_rules.ContainsKey(((IOwnedModel)rule).Owner.GetKey()))
                    _rules[((IOwnedModel)rule).Owner.GetKey()].AddRule(rule);
                else
                {
                    var o = new OrgUnitDataAccessSecurity(((IOwnedModel)rule).Owner.GetKey(), AppContext.Current.Container.GetInstance<IRuleCompiler>());
                    o.AddRule(rule);
                    _rules.Add(((IOwnedModel)rule).Owner.GetKey(), o);
                }
            }
        }

        public void RemoveRule(IRule rule)
        {
            lock (_rules)
            {
                _rules[((IOwnedModel)rule).Owner.GetKey()].Remove(rule);
            }
        }

        public bool Demand(IUser user, IModel model, Type modelType, DataActions action, 
            out string failureMessage, string orgUnitCallContext = null, IEnumerable<AuditedChange> changes = null, string customArg = "")
        {
            if (!modelType.Implements<IModel>())
                throw new InvalidOperationException("ModelType must implement IModel.");
            var demand = typeof(DataAccessSecurityContext).GetMethods(BindingFlags.Public | BindingFlags.Instance)
                                                          .Single(mi => mi.Name.Equals("Demand") && mi.IsGenericMethodDefinition)
                                                          .MakeGenericMethod(modelType);
            var fMessage = "";
            var args = new object[]
            {
                user,
                model,
                action,
                fMessage,
                orgUnitCallContext,
                changes,
                customArg
            };
            var result = demand.Invoke(this, args);
            failureMessage = (string)args[3];
            return (bool)result;
        }


        public bool Demand<T>(IUser user, T model, DataActions action, out string failureMessage, 
            string orgUnitCallContext = null, IEnumerable<AuditedChange> changes = null, string customArg = "") where T : IModel
        {
            if (IsEnabled && IsInitialized)
            {
                try
                {
                    var orgUnitDass = new List<OrgUnitDataAccessSecurity>();
                    var orgUnits = new HashSet<string>();

                    AddOrgUnit(orgUnits, model);

                    if (orgUnitCallContext != null && !orgUnits.Contains(orgUnitCallContext))
                    {
                        orgUnits.Add(orgUnitCallContext);
                    }

                    if (model is ILink)
                    {
                        AddOrgUnit(orgUnits, ((ILink)model).To);
                        AddOrgUnit(orgUnits, ((ILink)model).From);
                    }

                    var modelType = typeof(T);
                    if ((modelType.Equals(typeof(IModel)) || modelType.Equals(typeof(ILink))) && model != null)
                    {
                        modelType = model.ModelType;
                    }
                    else if (modelType.Implements<IPath>() && model != null)
                    {
                        modelType = ((IPath)model).Root.ModelType;
                    }
                    else if (modelType.Equals(typeof(IAny)) && model != null && !model.ModelType.Equals(typeof(IAny)))
                    {
                        modelType = model.ModelType;
                    }

                    var runtime = AppContext.Current.Container.GetInstance<IRuntimeBuilder>().Create(action, orgUnitCallContext, user, model, modelType, changes, customArg);
                    // test all applicable org unit rules, and ensure they ALL pass the test

                    var msg = "";
                    var oudass = _rules.Values.Where(oudas => orgUnits.Contains(oudas.OrgUnitKey)).ToArray();
                    var result = oudass.Length > 0 && oudass.All(oudas => oudas.Demand(runtime, EnforcementType, out msg));
                    failureMessage = msg;
                    return result;
                }
                catch (Exception ex)
                {
                    Logger.Log(ex);
                    failureMessage = ex.Message;
                    return false;
                }
            }
            else
            {
                failureMessage = "";
                return true;
            }
        }

        private void AddOrgUnit(HashSet<string> orgUnits, IModel model)
        {
            var names = new List<string>();
            if (model == null)
            {
                names.AddRange(((IClaimsPrincipal)SecurityContext.Current.CurrentPrincipal).GetOrgUnits().Select(ou => ou.GetKey()));
            }
            else
            {
                var name = "";
                if (!(model is IOwnedModel) || ((IOwnedModel)model)?.Owner == null)
                {
                    name = IOrgUnitDefaults.RootOrgUnit.GetKey();
                }
                else
                {
                    name = ((IOwnedModel)model)?.Owner.GetKey();
                }
                names.Add(name);
            }
            foreach (var name in names)
            {
                if (!orgUnits.Contains(name))
                {
                    orgUnits.Add(name);
                }
            }
        }

        private class OrgUnitDataAccessSecurity
        {
            Dictionary<string, ICompiledRule[]> _allowRules = new Dictionary<string, ICompiledRule[]>();
            Dictionary<string, ICompiledRule[]> _denyRules = new Dictionary<string, ICompiledRule[]>();
            public OrgUnitDataAccessSecurity(string orgUnitName, IRuleCompiler compiler)
            {
                this.Compiler = compiler;
                this.OrgUnitKey = orgUnitName;
            }

            private void Compile(IRule rule)
            {
                IEnumerable<ICompiledRule> compiledRules;

                if (Compiler.Compile(rule, out compiledRules))
                {
                    if (_allowRules.ContainsKey(rule.Name))
                    {
                        _allowRules.Remove(rule.Name);
                    }
                    _allowRules.Add(rule.Name, compiledRules.Where(cr => cr.Entitlement == Entitlement.Allow).ToArray());
                    if (_denyRules.ContainsKey(rule.Name))
                    {
                        _denyRules.Remove(rule.Name);
                    }
                    _denyRules.Add(rule.Name, compiledRules.Where(cr => cr.Entitlement == Entitlement.Deny).ToArray());
                }
                else
                {
                    Logger.LogError(Compiler.LastError, string.Format("The rule '{0}' could not be compiled due to an error.", rule.Name));
                }
            }

            public string OrgUnitKey { get; private set; }
            public IRuleCompiler Compiler { get; private set; }

            public bool Demand(IRuntime runtime, EnforcementType enforcementType, out string failureMessage)
            {
                failureMessage = "";
                DataAccessSecurityContext.Current.IsEnabled = false;
                var audit = AuditSettings.IsEnabled;
                AuditSettings.IsEnabled = false;
                try
                {
                    // run denies first, bail on first passing test
                    foreach (var cr in _denyRules.Values.SelectMany(ca => ca.Where(x => true)).Where(ca => ca.AppliesTo(runtime)))
                    {
                        if (cr.UserIsEntitled(runtime) && cr.ModelIsEntitled(runtime))
                        {
                            failureMessage = cr.FailureMessage ?? "The current user is not authorized to perform this action";
                            return false;
                        }
                    }

                    var isAllowed = true;
                    var crs = _allowRules.Values.SelectMany(cr => cr.Where(x => true)).Where(cr => cr.AppliesTo(runtime)).ToArray();
                    if (enforcementType == EnforcementType.Optimistic)
                    {
                        foreach (var cr in crs)
                        {
                            if (cr.UserIsEntitled(runtime) && cr.ModelIsEntitled(runtime))
                            {
                                // optimistic means only one rule must pass
                                return true;
                            }
                            failureMessage = cr.FailureMessage;
                        }
                        isAllowed = false;
                    }
                    else
                    {
                        foreach (var cr in crs)
                        {
                            if (!cr.UserIsEntitled(runtime) || !cr.ModelIsEntitled(runtime))
                            {
                                // pessimistic means if any rule fails, we fail
                                failureMessage = cr.FailureMessage ?? "The current user is not authorized to perform this action";
                                isAllowed = false;
                                break;
                            }
                        }
                    }

                    return isAllowed;
                }
                finally
                {
                    DataAccessSecurityContext.Current.IsEnabled = true;
                    AuditSettings.IsEnabled = audit;
                }
            }

            internal void Remove(IRule rule)
            {
                if (_rules.ContainsKey(rule.Name))
                {
                    _rules.Remove(rule.Name);
                }
            }

            internal void AddRule(IRule rule)
            {
                Remove(rule);
                Compile(rule);
            }
        }
    }
}

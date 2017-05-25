using Common;
using Common.Security;
using Data.Core.Evaluation;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Data.Core.Security
{
    public class CompiledRule<T> : ICompiledRule<T> where T : IRuntime
    {
        private Func<T, bool> _appliesToFunc;
        private Func<T, bool> _userEvaluator;
        private Func<T, bool> _modelEvaluator;


        public CompiledRule(IRule rule, IRulePolicy policy, Type modelType, Func<T, bool> userEvaluator, Func<T, bool> modelEvaluator, Func<T, bool> appliesToEvaluator)
        {
            Description = rule.Description;
            Name = rule.Name;
            EffectiveStartDate = rule.EffectiveStartDate;
            EffectiveEndDate = rule.EffectiveEndDate;
            Entitlement = policy.Entitlement;
            Actions = policy.Actions;
            FailureMessage = policy.FailureMessage;
            Rank = rule.Rank;


            _appliesToFunc = (runtime) =>
            {
                return runtime.ModelType.Implements(modelType) &&
                        runtime.Now >= EffectiveStartDate &&
                        runtime.Now <= EffectiveEndDate &&
                        policy.Actions.HasFlag(runtime.Action) &&
                        appliesToEvaluator(runtime);
            };
            _userEvaluator = (runtime) =>
            {
                return runtime.Now >= EffectiveStartDate &&
                        runtime.Now <= EffectiveEndDate &&
                        policy.Actions.HasFlag(runtime.Action) &&
                        userEvaluator(runtime);
            };
            _modelEvaluator = (runtime) =>
            {
                return runtime.Now >= EffectiveStartDate &&
                        runtime.Now <= EffectiveEndDate &&
                        policy.Actions.HasFlag(runtime.Action) &&
                        modelEvaluator(runtime);
            };
        }

        public DataActions Actions
        {
            get;
            private set;
        }

        public string Description
        {
            get;
            private set;
        }

        public DateTime EffectiveStartDate
        {
            get;
            private set;
        }

        public DateTime EffectiveEndDate
        {
            get;
            private set;
        }

        public Entitlement Entitlement
        {
            get;
            private set;
        }

        public string Name
        {
            get;
            private set;
        }
        public string FailureMessage
        {
            get;
            private set;
        }
        public int Rank
        {
            get;
            private set;
        }

        public bool ModelIsEntitled(T runtime)
        {
            return _modelEvaluator(runtime);
        }

        public bool UserIsEntitled(T runtime)
        {
            return _userEvaluator(runtime);
        }

        public bool UserIsEntitled(IRuntime runtime)
        {
            return UserIsEntitled((T)runtime);
        }

        public bool ModelIsEntitled(IRuntime runtime)
        {
            return ModelIsEntitled((T)runtime);
        }

        public bool AppliesTo(T runtime)
        {
            return _appliesToFunc(runtime);
        }

        public bool AppliesTo(IRuntime runtime)
        {
            return AppliesTo((T)runtime);
        }
    }
}

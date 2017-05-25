using Common.Security;
using Data.Core.Evaluation;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Data.Core.Security
{
    public interface ICompiledRule<T> : ICompiledRule where T : IRuntime
    {
        bool UserIsEntitled(T runtime);
        bool ModelIsEntitled(T runtime);
        bool AppliesTo(T runtime);
    }

    public interface ICompiledRule
    {
        DataActions Actions { get; }
        Entitlement Entitlement { get; }
        string Name { get; }
        string Description { get; }
        DateTime EffectiveStartDate { get; }
        DateTime EffectiveEndDate { get; }
        int Rank { get; }
        string FailureMessage { get; }
        bool UserIsEntitled(IRuntime runtime);
        bool ModelIsEntitled(IRuntime runtime);
        bool AppliesTo(IRuntime runtime);
    }
}

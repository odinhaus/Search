using Data.Core.Evaluation;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Data.Core.Security
{
    public interface IRuleCompiler<T> : IRuleCompiler where T : IRuntime
    {
        bool Compile(IRule rule, out IEnumerable<ICompiledRule<T>> compiledRules);
    }

    public interface IRuleCompiler
    {
        bool Compile(IRule rule, out IEnumerable<ICompiledRule> compiledRules);
        Exception LastError { get; }
    }
}

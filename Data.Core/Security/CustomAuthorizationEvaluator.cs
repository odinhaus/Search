using Common.Security;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Data.Core.Security
{
    public class CustomAuthorizationEvaluator : ICustomAuthorizationEvaluator
    {
        public CustomAuthorizationEvaluator(Func<bool> evaluator)
        {
            this.Evaluator = evaluator;
        }

        public Func<bool> Evaluator { get; private set; }

        public bool Demand()
        {
            return Evaluator();
        }
    }
}

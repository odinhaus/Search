using Common.Security;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Data.Core.Security
{
    public class UnrestrictedAuthorizationEvaluator : ICustomAuthorizationEvaluator
    {
        public bool Demand()
        {
            return true;
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Data.Core.Dynamic
{
    public interface IDynamicFunctionEvaluator
    {
        object Execute(string methodName, object[] args);
    }
}

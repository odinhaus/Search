using Common.Security;
using Data.Core.Auditing;
using Data.Core.Security;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Data.Core.Evaluation
{
    public class RuntimeBuilder : IRuntimeBuilder
    {
        public IRuntime Create(DataActions action, string callContextOrgUnit, IUser currentUser, IModel currentModel, Type modelType, IEnumerable<AuditedChange> changes, string customArg = "")
        {
            if (modelType == null)
                throw new InvalidOperationException("The modelType cannot be null.");
            return new Runtime(action, callContextOrgUnit, currentUser, currentModel, modelType, changes, customArg);
        }
    }
}

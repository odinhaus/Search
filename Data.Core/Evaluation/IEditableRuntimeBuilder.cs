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
    public interface IEditableRuntimeBuilder
    {
        IEditableRuntime Create(DataActions action, string callContextOrgUnit, IUser currentUser, IModel currentModel, Type modelType, IEnumerable<AuditedChange> changes, string customArg = "");
    }
}

using Data.Core.Security;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Principal;
using System.Text;
using System.Threading.Tasks;

namespace Data.Core
{
    public interface IAuditer
    {
        void Audit<T>(IIdentity user, IEnumerable<T> models, AuditEventType eventType, string additionalData = null);
        void Audit(IIdentity user, IEnumerable<IModel> models, AuditEventType eventType, string additionalData = null);

        ModelList<IAny> History(string globalModelKey, int offset, int pageSize);
    }
}

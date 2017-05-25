using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Common.Auditing
{
    public interface IHttpAuditScopeTokenWriter
    {
        string Write(out string httpHeader);
    }
}

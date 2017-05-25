using Common.Auditing;
using Common.Security;
using Data.Core.Linq.Win;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Data.Core.Auditing
{
    public class HttpAuditScopeRepositoryWorkContextTokenWriter : IHttpAuditScopeTokenWriter
    {
        public string Write(out string httpHeader)
        {
            httpHeader = HttpAuditScopeTokenDefaults.HTTP_HEADER;
            return ClientWorkRepository.Current?.WorkContextKey ?? SecurityContext.Current.ScopeId;
        }
    }
}

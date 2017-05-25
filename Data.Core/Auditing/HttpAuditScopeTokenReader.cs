using Common.Auditing;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace Data.Core.Auditing
{
    public class HttpAuditScopeTokenReader : IHttpAuditScopeTokenReader
    {
        public bool ReadToken(HttpRequestMessage request, out string token)
        {
            token = Guid.NewGuid().ToString();
            if (request.Headers.Contains(HttpAuditScopeTokenDefaults.HTTP_HEADER))
            {
                token = request.Headers.GetValues(HttpAuditScopeTokenDefaults.HTTP_HEADER).First();
                return true;
            }
            return false;
        }
    }
}

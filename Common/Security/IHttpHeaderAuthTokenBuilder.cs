using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace Common.Security
{
    public interface IHttpHeaderAuthTokenBuilder
    {
        string GetRequestToken(out string headerName);
        bool ReadHeader(HttpRequestMessage httpRequest, out string token, out TokenType tokenType);
    }
}

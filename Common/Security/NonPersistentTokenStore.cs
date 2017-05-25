using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Principal;
using System.Text;
using System.Threading.Tasks;
using Microsoft.IdentityModel.Claims;

namespace Common.Security
{
    public class NonPersistentTokenStore : ITokenStore
    {
        public void DeleteToken(IClaimsIdentity identity, TokenType tokenType)
        {
        }

        public string GetToken(string username, TokenType tokenType, out DateTime expiration, out IClaimsIdentity identity)
        {
            throw new NotSupportedException();
        }

        public void SaveToken(IClaimsIdentity identity, TokenType tokenType, string token, DateTime expiration)
        {
        }


        public bool TryGetToken(string username, TokenType tokenType, out string token, out DateTime expiration, out IClaimsIdentity identity)
        {
            token = null;
            expiration = DateTime.MinValue;
            identity = null;
            return false;
        }
    }
}

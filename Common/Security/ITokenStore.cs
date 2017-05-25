using Microsoft.IdentityModel.Claims;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Principal;
using System.Text;
using System.Threading.Tasks;

namespace Common.Security
{
    public interface ITokenStore
    {
        /// <summary>
        /// Gets the cached security token for the current windows user
        /// </summary>
        /// <returns></returns>
        bool TryGetToken(string username, TokenType tokenType, out string token, out DateTime expiration, out IClaimsIdentity identity);
        /// <summary>
        /// Gets the cached security token for the current windows user
        /// </summary>
        /// <returns></returns>
        string GetToken(string username, TokenType tokenType, out DateTime expiration, out IClaimsIdentity identity);
        /// <summary>
        /// Stores the provided token for the current windows user
        /// </summary>
        /// <param name="token"></param>
        void SaveToken(IClaimsIdentity identity, TokenType tokenType, string token, DateTime expiration);
        /// <summary>
        /// Deletes the security token for the current windows user, if it exists
        /// </summary>
        /// <param name="tokenType"></param>
        void DeleteToken(IClaimsIdentity identity, TokenType tokenType);
    }
}

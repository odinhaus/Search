using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Common.Licensing
{
    public interface ILicense
    {
        /// <summary>
        /// Returns the value of an embedded token within the license data.  Token values can be used 
        /// in variety of ways, and are determined by the licensing implementer.  Examples inlucde numeric values 
        /// that might indicate the number of concurrent users allowed, or CPU cores to use, or whether a certain 
        /// named feature is enabled, etc.  Return types can be simple scalar fields or complex types.
        /// </summary>
        /// <typeparam name="T">the token value type, as provided by the licensing strategy</typeparam>
        /// <param name="tokenName">the name of the token</param>
        /// <returns>the token value</returns>
        bool TryGetToken<T>(string tokenName, out T tokenValue);

        /// <summary>
        /// Returns an enumerable of type T matching the tokenName provided.  If the token is not found, the 
        /// enumeration returns an empty set.
        /// </summary>
        /// <param name="tokenName"></param>
        /// <returns></returns>
        IEnumerable<T> GetTokens<T>(string tokenName);

        /// <summary>
        /// the serial number portion of the license that uniquely identifies the instance of the implemented license
        /// </summary>
        string Key { get; }

        /// <summary>
        /// the license's validation code to confirm that the license data has not be aletered after issuance
        /// </summary>
        string ValidationCode { get; }

        /// <summary>
        /// indicated whether the license appears to be valid
        /// </summary>
        bool IsValid { get; }

        /// <summary>
        /// Date the license was issued by the vendor
        /// </summary>
        DateTime IssuedDate { get; }

        /// <summary>
        /// Date when this license will expire.  For non-expiring licenses, this value should be DateTime.MaxValue.
        /// </summary>
        DateTime ExpirationDate { get; }
    }
}

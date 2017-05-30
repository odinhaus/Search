using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Suffuz.Identity
{
    public static class Hashing
    {
        public static string ToBase64SHA256(this string value)
        {
            var hasher = new System.Security.Cryptography.SHA256CryptoServiceProvider();
            return System.Convert.ToBase64String(hasher.ComputeHash(UTF8Encoding.UTF8.GetBytes(value)));
        }
    }
}

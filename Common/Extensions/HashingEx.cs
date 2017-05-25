using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Common.Extensions
{
    public static class HashingEx
    {
        public static string ToBase64SHA1(this string value)
        {
            var hasher = System.Security.Cryptography.SHA1.Create();
            return System.Convert.ToBase64String(hasher.ComputeHash(UTF8Encoding.UTF8.GetBytes(value)));
        }

        public static string ToBase64MD5(this string value)
        {
            var hasher = System.Security.Cryptography.MD5.Create();
            return System.Convert.ToBase64String(hasher.ComputeHash(UTF8Encoding.UTF8.GetBytes(value)));
        }

        public static string ToBase64SHA256(this string value)
        {
            var hasher = new System.Security.Cryptography.SHA256CryptoServiceProvider();
            return System.Convert.ToBase64String(hasher.ComputeHash(UTF8Encoding.UTF8.GetBytes(value)));
        }

        public static string ToBase64SHA1(this byte[] value)
        {
            var hasher = System.Security.Cryptography.SHA1.Create();
            return System.Convert.ToBase64String(hasher.ComputeHash(value));
        }

        public static string ToBase64MD5(this byte[] value)
        {
            var hasher = System.Security.Cryptography.MD5.Create();
            return System.Convert.ToBase64String(hasher.ComputeHash(value));
        }

        public static string ToBase64SHA256(this byte[] value)
        {
            var hasher = new System.Security.Cryptography.SHA256CryptoServiceProvider();
            return System.Convert.ToBase64String(hasher.ComputeHash(value));
        }
    }
}

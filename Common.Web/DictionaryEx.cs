using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Web;

namespace Common.Web
{
    public static class DictionaryEx 
    {
        public static string ToUrlEncodedFormData(this Dictionary<string, string> values)
        {
            var sb = new StringBuilder();
            foreach(var kvp in values)
            {
                if (sb.Length > 0)
                {
                    sb.Append("&");
                }
                sb.Append(HttpUtility.UrlEncode(kvp.Key));
                sb.Append("=");
                sb.Append(HttpUtility.UrlEncode(kvp.Value));
            }
            return sb.ToString();
        }
    }
}

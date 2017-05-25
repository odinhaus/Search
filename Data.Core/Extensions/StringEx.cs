using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Common
{
    public static class StringEx
    {
        public static string StripSpecial(this string source, string replacement)
        {
            Regex r = new Regex(@"\W");
            return r.Replace(source, replacement);
        }
    }
}

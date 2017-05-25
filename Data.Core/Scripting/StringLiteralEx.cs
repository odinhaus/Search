using Antlr4.Runtime.Tree;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Data.Core.Scripting
{
    public static class StringLiteralEx
    {
        public static string GetString(this IParseTree ctx)
        {
            var value = ctx.GetText();
            value = value.Substring(1, value.Length - 2).Replace("\\'", "'").Replace("\\\"", "\"");
            return value;
        }
    }
}

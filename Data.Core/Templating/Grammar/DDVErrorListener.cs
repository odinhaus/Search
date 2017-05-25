using Antlr4.Runtime;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Data.Core.Templating.Grammar
{
    public class DDVErrorListener : IAntlrErrorListener<IToken>
    {
        public void SyntaxError(IRecognizer recognizer, IToken offendingSymbol, int line, int charPositionInLine, string msg, RecognitionException e)
        {
            Error = "Error in parser at line " + e?.OffendingToken?.Line ?? "<unknown>" + ":" + e?.OffendingToken?.Column ?? "<unknown>" + e?.Message ?? "error";
            Console.WriteLine(Error);
        }

        public string Error { get; private set; }
    }
}

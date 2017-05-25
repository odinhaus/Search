using Antlr4.Runtime;
using Antlr4.Runtime.Tree;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Data.Core.Security.Grammar
{
    public static class ACSL
    {
        public static bool Validate(string query, out IEnumerable<ACSLError> errors)
        {
            return Validate(query, (parser) => parser.logicalExpression(), out errors);
        }


        public static bool Validate(string query, Func<ACSLParser, ParserRuleContext> parserRuleSelector, out IEnumerable<ACSLError> errors)
        {
            errors = new List<ACSLError>();
            var errorListener = new ACSLErrorListener();

            var inputStream = new AntlrInputStream(query);
            var helloLexer = new ACSLLexer(inputStream);
            var commonTokenStream = new CommonTokenStream(helloLexer);
            var parser = new ACSLParser(commonTokenStream);

            parser.AddErrorListener(errorListener);
            var ast = parserRuleSelector(parser);
            var e = (IList<ACSLError>)errors;
            ReportErrors(ast, e);
            return errors.Count() == 0;
        }

        public static ACSLParser.LogicalExpressionContext Parse(string query)
        {
            return CreateParser(query).logicalExpression();
        }

        public static ACSLParser CreateParser(string query)
        {
            var inputStream = new AntlrInputStream(query);
            var helloLexer = new ACSLLexer(inputStream);
            var commonTokenStream = new CommonTokenStream(helloLexer);
            var parser = new ACSLParser(commonTokenStream);
            return parser;
        }

        static void ReportErrors(IParseTree context, IList<ACSLError> errors)
        {
            if (context is ParserRuleContext && ((ParserRuleContext)context).children != null)
            {
                foreach (var child in ((ParserRuleContext)context).children)
                {
                    ReportErrors(child, errors);
                }
            }
            if (context is ErrorNodeImpl)
            {
                errors.Add(new ACSLError(((ErrorNodeImpl)context).Payload.Line, ((ErrorNodeImpl)context).Payload.Column, ((ErrorNodeImpl)context).GetText()));
            }
        }

        public class ACSLError
        {
            public ACSLError(int line, int character, string error)
            {
                this.Line = line;
                this.Character = character;
                this.Error = error;
            }
            public int Line { get; private set; }
            public int Character { get; private set; }
            public string Error { get; private set; }
        }
    }
}

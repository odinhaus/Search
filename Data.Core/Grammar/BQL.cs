using Antlr4.Runtime;
using Antlr4.Runtime.Tree;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Data.Core.Grammar
{
    public static class BQL
    {
        public static bool Validate(string query, out IEnumerable<BQLError> errors)
        {
            errors = new List<BQLError>();
            var errorListener = new BQLErrorListener();

            var inputStream = new AntlrInputStream(query);
            var helloLexer = new BQLLexer(inputStream);
            var commonTokenStream = new CommonTokenStream(helloLexer);
            var parser = new BQLParser(commonTokenStream);

            parser.AddErrorListener(errorListener);
            var ast = parser.queryExpression();
            var e = (IList<BQLError>)errors;
            ReportErrors(ast, e);
            return errors.Count() == 0;
        }

        public static BQLParser.QueryExpressionContext Parse(string query)
        {
            return CreateParser(query).queryExpression();
        }

        public static BQLParser CreateParser(string query)
        {
            var inputStream = new AntlrInputStream(query);
            var helloLexer = new BQLLexer(inputStream);
            var commonTokenStream = new CommonTokenStream(helloLexer);
            var parser = new BQLParser(commonTokenStream);
            return parser;
        }

        static void ReportErrors(IParseTree context, IList<BQLError> errors)
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
                errors.Add(new BQLError(((ErrorNodeImpl)context).Payload.Line, ((ErrorNodeImpl)context).Payload.Column, ((ErrorNodeImpl)context).GetText()));
            }
        }

        public class BQLError
        {
            public BQLError(int line, int character, string error)
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

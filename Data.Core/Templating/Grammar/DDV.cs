using Antlr4.Runtime;
using Antlr4.Runtime.Tree;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Data.Core.Templating.Grammar
{
    public static class DDV
    {
        public static bool Validate(string query, out IEnumerable<DDVError> errors)
        {
            return Validate(query, (parser) => parser.codeStatement(), out errors);
        }


        public static bool Validate(string query, Func<DDVParser, ParserRuleContext> parserRuleSelector, out IEnumerable<DDVError> errors)
        {
            errors = new List<DDVError>();
            var errorListener = new DDVErrorListener();

            var inputStream = new AntlrInputStream(query);
            var helloLexer = new DDVLexer(inputStream);
            var commonTokenStream = new CommonTokenStream(helloLexer);
            var parser = new DDVParser(commonTokenStream);

            parser.AddErrorListener(errorListener);
            var ast = parserRuleSelector(parser);
            var e = (IList<DDVError>)errors;
            ReportErrors(ast, e);
            return errors.Count() == 0;
        }

        public static DDVParser.CodeStatementContext Parse(string query)
        {
            return CreateParser(query).codeStatement();
        }

        public static DDVParser CreateParser(string query)
        {
            var inputStream = new AntlrInputStream(query);
            var helloLexer = new DDVLexer(inputStream);
            var commonTokenStream = new CommonTokenStream(helloLexer);
            var parser = new DDVParser(commonTokenStream);
            return parser;
        }

        static void ReportErrors(IParseTree context, IList<DDVError> errors)
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
                errors.Add(new DDVError(((ErrorNodeImpl)context).Payload.Line, ((ErrorNodeImpl)context).Payload.Column, ((ErrorNodeImpl)context).GetText()));
            }
        }

        public class DDVError
        {
            public DDVError(int line, int character, string error)
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

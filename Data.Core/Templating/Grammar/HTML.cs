using Antlr4.Runtime;
using Antlr4.Runtime.Tree;
using Common;
using Common.Security;
using Data.Core.Evaluation;
using Data.Core.Security;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Data.Core.Templating.Grammar
{
    public static class HTML
    {
        public static bool Validate(string html, out IEnumerable<HTMLError> errors)
        {
            return Validate(html, (parser) => parser.htmlDocument(), out errors);
        }

        public static bool Evaluate<T>(string templateName, string html, T model, out string finalHtml, out IEnumerable<HTMLError> errors) where T : IModel
        {
            finalHtml = html;
            errors = null;
            try
            {
                if (Validate(html, out errors))
                {
                    var runtime = AppContext.Current.Container.GetInstance<IRuntimeBuilder>().Create(
                        Common.Security.DataActions.Read,
                        null,
                        SecurityContext.Current.ToUser(),
                        model,
                        typeof(T),
                        new Auditing.AuditedChange[0]);
                    finalHtml = DDVTemplateEvaluator.Evaluate<T>(html, templateName)(runtime);
                    return true;
                }
                else
                {
                    return false;
                }
            }
            catch (Exception ex)
            {
                errors = new HTMLError[] { new HTMLError(-1, -1, ex.ToString()) };
                return false;
            }
        }


        public static bool Validate(string query, Func<HTMLParser, ParserRuleContext> parserRuleSelector, out IEnumerable<HTMLError> errors)
        {
            errors = new List<HTMLError>();
            var errorListener = new DDVErrorListener();

            var inputStream = new AntlrInputStream(query);
            var helloLexer = new HTMLLexer(inputStream);
            var commonTokenStream = new CommonTokenStream(helloLexer);
            var parser = new HTMLParser(commonTokenStream);

            parser.AddErrorListener(errorListener);
            var ast = parserRuleSelector(parser);
            var e = (IList<HTMLError>)errors;
            ReportErrors(ast, e);
            return errors.Count() == 0;
        }

        public static HTMLParser.HtmlDocumentContext Parse(string query)
        {
            return CreateParser(query).htmlDocument();
        }

        public static HTMLParser CreateParser(string query)
        {
            var inputStream = new AntlrInputStream(query);
            var helloLexer = new HTMLLexer(inputStream);
            var commonTokenStream = new CommonTokenStream(helloLexer);
            var parser = new HTMLParser(commonTokenStream);
            return parser;
        }

        static void ReportErrors(IParseTree context, IList<HTMLError> errors)
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
                errors.Add(new HTMLError(((ErrorNodeImpl)context).Payload.Line, ((ErrorNodeImpl)context).Payload.Column, ((ErrorNodeImpl)context).GetText()));
            }
        }

        public class HTMLError
        {
            public HTMLError(int line, int character, string error)
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

    public static class HTMLErrorCollectionEx
    {
        public static string ToErrorString(this IEnumerable<HTML.HTMLError> errors)
        {
            // log failure
            StringBuilder sb = new StringBuilder("");
            foreach (var error in errors)
            {
                // LogError(results.Errors[i].ErrorText);
                sb.AppendLine(String.Format("Error: {0},  Line: {1}, Character: {2}",
                    error.Error,
                    error.Line,
                    error.Character));
            }
            return sb.ToString();
        }
    }
}

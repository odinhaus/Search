using Antlr4.Runtime.Tree;
using Common;
using Common.Extensions;
using Data.Core.Evaluation;
using Data.Core.Scripting;
using Data.Core.Templating.Grammar;
using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace Data.Core.Templating
{
    public class DDVTemplateEvaluator
    {
        static long _counter = 1;

        public static Func<IRuntime, string> Evaluate<T>(string templateText, string templateName) where T : IModel
        {
            HTMLParser.HtmlDocumentContext ctx = null;
            IEnumerable<HTML.HTMLError> errors;
            if (HTML.Validate(templateText, (parser) => { ctx = parser.htmlDocument(); return ctx; }, out errors))
            {
                TemplateContext codeContext;
                VariableScope.Clear();
                PartialEvaluate<T>(ctx, templateName, out codeContext);
                codeContext.CompleteScope(true);
                return (Func<IRuntime, string>)codeContext.CompletedScope;
            }
            throw new InvalidOperationException("The template is not valid: " + errors.ToErrorString());
        }

        internal static Expression PartialEvaluate<T>(IParseTree context, string name, out TemplateContext codeContext) where T : IModel
        {
            return PartialEvaluate(context, name, typeof(T), context.GetText().ToBase64MD5(), out codeContext);
        }

        private static Expression PartialEvaluate(IParseTree context, string name, Type modelType, string checksum, out TemplateContext codeContext)
        {
            var runtime = AppContext.Current.Container.GetInstance<IRuntimeBuilder>().Create(Common.Security.DataActions.Read, null, null, null, modelType, null);
            using (codeContext = TemplateContext.Create(name, runtime.GetType(), modelType, checksum))
            {
                if (!codeContext.IsCompleted)
                {
                    var visitor = new DDVTemplateEvaluator(context, codeContext, modelType);
                    visitor.Evaluate();
                }
                return codeContext.SubScope();
            }
        }

        private DDVTemplateEvaluator(IParseTree context, TemplateContext codeContext, Type modelType)
        {
            this.Root = context;
            this.CodeContext = codeContext;
            this.ModelType = modelType;
            this.InlineMatch = new Regex(@"\[\[(?<inline>.*?)\]\]");
        }

        public IParseTree Root { get; private set; }
        public TemplateContext CodeContext { get; private set; }
        public Regex InlineMatch { get; private set; }
        public Type ModelType { get; private set; }

        private void Evaluate()
        {
            this.Visit(Root);
        }

        private void Visit(IParseTree context)
        {
            var text = context.GetText();
            if (context is HTMLParser.ScriptletContext && text.StartsWith("<ddv>"))
            {
                IEnumerable<DDV.DDVError> errors;
                if (DDV.Validate(text, out errors))
                {
                    this.CodeContext.AppendCode(DDV.Parse(text));
                    return;
                }
                else
                {
                    this.CodeContext.AppendText("<!-- ");

                    foreach (var error in errors)
                    {
                        this.CodeContext.AppendText(string.Format("Code Parsing Error: line {0}, character {1}, {2};", error.Line, error.Character, error.Error));
                    }

                    this.CodeContext.AppendText(" -->");
                }
            }
            else if (context is HTMLParser.HtmlElementContext)
            {
                for (int i = 0; i < context.ChildCount; i++)
                {
                    var child = context.GetChild(i);
                    if (child is HTMLParser.HtmlAttributeContext)
                    {
                        for (int j = 0; j < child.ChildCount; j++)
                        {
                            var attrChild = child.GetChild(j);
                            if (attrChild is HTMLParser.HtmlAttributeNameContext)
                            {
                                var attribName = child.GetChild(0).GetText();

                                switch (attribName.ToLower())
                                {
                                    case "ddv-repeat":
                                        {
                                            EvaluateRepeat(context as HTMLParser.HtmlElementContext, i, child as HTMLParser.HtmlAttributeContext, j);
                                            return;
                                        }
                                    case "ddv-if":
                                        {
                                            EvaluateIf(context as HTMLParser.HtmlElementContext, i, child as HTMLParser.HtmlAttributeContext, j);
                                            return;
                                        }
                                    default: { break; }
                                }
                            }
                        }
                    }
                }
            }
            else if (context is HTMLParser.HtmlAttributeNameContext)
            {
                this.CodeContext.AppendText(" ");
            }
            else if (context is HTMLParser.HtmlAttributeValueContext || context is HTMLParser.HtmlChardataContext)
            {
                var value = context.GetText();
                var matches = InlineMatch.Matches(value);
                if (matches.Count > 0)
                {
                    var start = 0;
                    foreach (var match in matches.Cast<Match>())
                    {
                        var txt = value.Substring(start, match.Index - start);
                        this.CodeContext.AppendText(txt);
                        this.CodeContext.AppendInline(match.Groups["inline"].Value);
                        start = match.Index + match.Length;
                    }
                    this.CodeContext.AppendText(value.Substring(start, value.Length - start));
                    return;
                }
            }


            if (context.ChildCount == 0)
            {
                this.CodeContext.AppendText(text);
            }
            else
            {
                for (int i = 0; i < context.ChildCount; i++)
                {
                    Visit(context.GetChild(i));
                }
            }
        }

        private void EvaluateRepeat(HTMLParser.HtmlElementContext repeatedElement, int dvRepeatAttributeIndex, HTMLParser.HtmlAttributeContext dvRepeatAttribute, int dvRepeatAttributeNameIndex)
        {
            var dvRepeatAttributeExpression = dvRepeatAttribute.GetChild(dvRepeatAttributeNameIndex + 2).GetChild(0).GetText();
            dvRepeatAttributeExpression = dvRepeatAttributeExpression.Substring(1, dvRepeatAttributeExpression.Length - 2).Replace("&lt;", "<").Replace("&gt;", ">");
            this.CodeContext.AppendFor(dvRepeatAttributeExpression, () =>
            {
                var counter = Interlocked.Increment(ref _counter);
                var name = CodeContext.Name + ".ForEach_" + counter;
                repeatedElement.children.Remove(dvRepeatAttribute);
                TemplateContext codeContext;
                return DDVTemplateEvaluator.PartialEvaluate(repeatedElement, name, ModelType, repeatedElement.GetText().ToBase64MD5(), out codeContext);
            });
        }

        private void EvaluateIf(HTMLParser.HtmlElementContext context, int dvIfAttributeIndex, HTMLParser.HtmlAttributeContext dvIfAttribute, int dvIfAttributeNameIndex)
        {
            var dvIfAttributeExpression = dvIfAttribute.GetChild(dvIfAttributeNameIndex + 2).GetChild(0).GetText();
            dvIfAttributeExpression = dvIfAttributeExpression.Substring(1, dvIfAttributeExpression.Length - 2).Replace("&lt;", "<").Replace("&gt;", ">");
            this.CodeContext.AppendIf(dvIfAttributeExpression, () =>
            {
                var counter = Interlocked.Increment(ref _counter);
                var name = CodeContext.Name + ".If_" + counter;
                context.children.Remove(dvIfAttribute);
                TemplateContext codeContext;
                return DDVTemplateEvaluator.PartialEvaluate(context, name, ModelType, context.GetText().ToBase64MD5(), out codeContext);
            });
        }

        public string Replace(string s, int index, int length, string replacement)
        {
            var builder = new StringBuilder();
            builder.Append(s.Substring(0, index));
            builder.Append(replacement);
            builder.Append(s.Substring(index + length));
            return builder.ToString();
        }
    }
}

using Antlr4.Runtime.Tree;
using Common;
using Data.Core.Compilation;
using Data.Core.Grammar;
using Data.Core.Linq;
using Data.Core.Evaluation;
using Data.Core.Templating.Grammar;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using Data.Core.Scripting;

namespace Data.Core.Templating
{
    public class TemplateContext : CodeContext
    {
        static Dictionary<string, TemplateContext> _contexts = new Dictionary<string, TemplateContext>();
        public new static TemplateContext Create(string name, Type runtimeType, Type modelType, string checksum)
        {
            TemplateContext context = null;
            lock (_contexts)
            {
                if (name == null || !_contexts.TryGetValue(name + modelType.FullName, out context) || !context.Checksum.Equals(checksum) || !context.IsCompleted)
                {
                    if (context != null)
                    {
                        _contexts.Remove(name + modelType.FullName);
                    }
                    context = new TemplateContext(name, runtimeType, modelType, checksum);

                    if (name != null)
                        _contexts.Add(name + modelType.FullName, context);
                }
            }
            return context;
        }

        protected TemplateContext(string name, Type runtimeType, Type modelType, string checksum) : base(name, runtimeType, modelType, checksum)
        {
            
        }

        protected override void Initialize(string name, Type runtimeType, Type modelType, string checksum)
        {
            if (!modelType.Implements<IModel>())
                throw new InvalidOperationException("The modelType must implement the IModel interface.");

            this.Checksum = checksum;
            this.Name = name;
            this.RuntimeType = runtimeType;
            this.ModelType = modelType;
            this.Statements = new List<Expression>();

            this.AppendMethod = typeof(StringBuilder).GetMethods(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public)
                                              .Single(mi => mi.Name.Equals("Append") && mi.GetParameters().Length == 1 && mi.GetParameters().All(p => p.ParameterType.Equals(typeof(string))));

            Expression iruntime, runtime = null;
            if (!TryGetParameter("@iruntime", out iruntime))
            {
                iruntime = Expression.Parameter(typeof(IRuntime), "@iruntime");
                runtime = Expression.Variable(RuntimeType, "@runtime");
                VariableScope.Current.Add("@iruntime", iruntime);
                VariableScope.Current.Add("@runtime", runtime);
                Statements.Add(Expression.Assign(runtime, Convert(iruntime, RuntimeType)));
            }
            else
            {
                TryGetParameter("@runtime", out runtime);
            }
            this.IRuntime = (ParameterExpression)iruntime;
            this.Runtime = (ParameterExpression)runtime;
            Expression sb = null;
            if (!TryGetParameter("@sb", out sb))
            {
                sb = Expression.Variable(typeof(StringBuilder), "@sb");
                VariableScope.Current.Add("@sb", sb); // adds to my current scope
                this.Statements.Add(Expression.Assign(sb, Expression.New(typeof(StringBuilder))));
            }
            this.StringBuilder = (ParameterExpression)sb;

            if (!VariableScope.Current.IsRoot)
                VariableScope.Push();
        }


        protected override void PreProcess()
        {
            ApplyPendingText();
        }

        private void Append(Expression exp)
        {
            if (exp.Type.Equals(typeof(string)))
                this.Statements.Add(Expression.Call(this.StringBuilder, this.AppendMethod, exp));
            else if (exp.Type.Equals(typeof(void)))
                this.Statements.Add(exp);
            else
            {
                var toString = typeof(object).GetMethod("ToString");
                Append(Expression.Call(Convert(exp, typeof(object)), toString));
            }
        }

        StringBuilder _inlineText = new System.Text.StringBuilder();
        public void AppendText(string text)
        {
            _inlineText.Append(text);
        }

        public void AppendInline(string inlineCode)
        {
            ApplyPendingText();
            IEnumerable<DDV.DDVError> errors;
            DDVParser.InlineStatementContext context = null;
            if (DDV.Validate(inlineCode, (parser) => { context = parser.inlineStatement(); return context; }, out errors))
            {
                Append(Visit(context));
            }
            else
            {
                AppendError(ErrorText(errors));
            }
        }

        private void ApplyPendingText()
        {
            if (_inlineText.Length > 0)
            {
                var text = _inlineText.ToString();
                _inlineText.Clear(); // need this to stop stack overflow
                Append(Expression.Constant(text));
            }
        }

        private void AppendError(string error, params object[] values)
        {
            PreProcess();
            AppendText(string.Format("<![CDATA[{0}]]>", string.Format(error, values)));
        }

        public override Expression CompleteScope(bool compile = false)
        {
            if (!IsCompleted)
            {
                // compile the function

                if (compile)
                {
                    try
                    {
                        ApplyPendingText();
                        IEnumerable<ParameterExpression> blkParms;
                        IEnumerable<Expression> blkExps;

                        // we need to add some statements to the end of the sub scope block in order to return the compiled text
                        // so we need to reconstitute the original expressions from the sub scope block
                        BreakBlock(SubScope() as BlockExpression, out blkParms, out blkExps);
                        this.Statements.Clear();
                        this.Statements.AddRange(blkExps);

                        var parms = new List<ParameterExpression>();
                        var returnLabel = Expression.Label(typeof(string), "return");
                        var toString = typeof(StringBuilder).GetMethods(BindingFlags.Public | BindingFlags.Instance)
                                                            .Single(mi => mi.Name.Equals("ToString") && mi.GetParameters().Length == 0);
                        var toStringCall = Expression.Call(this.StringBuilder, toString);

                        this.Statements.Add(toStringCall); // convert the string builder to string so we can return it

                        var result = Expression.Variable(typeof(string), "result");
                        var resultAssign = Expression.Assign(result, toStringCall);

                        this.Statements.Add(resultAssign);
                        this.Statements.Add(Expression.Return(returnLabel, result));
                        this.Statements.Add(Expression.Label(returnLabel, Expression.Constant("")));

                        VariableScope.Current.Add(result.Name, result);

                        parms.AddRange(VariableFinder.FindParameters(this.Statements));

                        var scope = CreateBlock(
                                            parms.ToArray(),
                                            this.Statements.ToArray());

                        var exceptionRef = Expression.Variable(typeof(Exception), "ex");
                        var exToString = Expression.Call(exceptionRef, typeof(object).GetMethod("ToString"));
                        // catch errors
                        var tryCatch = Expression.TryCatch(
                        scope,
                        Expression.Catch(exceptionRef,
                            Expression.Block(
                                Expression.Return(returnLabel, exToString),
                                Expression.Label(returnLabel, Expression.Constant("")))));
                        this.Statements.Clear();
                        this.Statements.Add(tryCatch);


                        this.CompletedScopeExpression = Expression.Lambda<Func<IRuntime, string>>(tryCatch, this.IRuntime);
                        this.CompletedScope = ((Expression<Func<IRuntime, string>>)CompletedScopeExpression).Compile();

                        IsCompleted = true;
                    }
                    finally
                    {
                        VariableScope.Clear();
                    }
                }
                else
                {
                    this.CompletedScopeExpression = SubScope();
                }
            }
            return CompletedScopeExpression;
        }

       
        public ParameterExpression StringBuilder { get; private set; }
        public NewExpression NewStringBuilder { get; private set; }


        protected override Expression VisitEmit(DDVParser.FunctionContext ctx)
        {
            var concat = typeof(string).GetMethods(BindingFlags.Public | BindingFlags.Static)
                                       .Single(mi => mi.Name.Equals("Concat") && mi.GetParameters()[0].ParameterType.Equals(typeof(string[])));

            var callConcat = Expression.Call(
                null,
                concat,
                Expression.NewArrayInit(typeof(string),
                    Expression.Constant("<!-- EMIT: "),
                    Convert(Visit(ctx.GetChild(2)), typeof(string)),
                    Expression.Constant(" -->")));

            return Expression.Call(this.StringBuilder, this.AppendMethod, callConcat);
        }
    }
}

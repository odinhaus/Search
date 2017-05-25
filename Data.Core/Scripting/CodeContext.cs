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
using System.Text.RegularExpressions;
using Antlr4.Runtime;
using Common.Serialization.Binary;
using Common.Serialization;

namespace Data.Core.Scripting
{
    public class CodeContext : IDisposable
    {
        static Dictionary<string, CodeContext> _contexts = new Dictionary<string, CodeContext>();
        [ThreadStatic]
        static Stack<string> _stackFrames;

        public static void ResetStack()
        {
            _stackFrames = new Stack<string>();
            lock (_objectDefs)
            {
                _objectDefs.Clear();
            }
        }

        public static void PushFrame(string code, int lineNumber, ConsoleColor color = ConsoleColor.Yellow)
        {
            Console.ForegroundColor = color;
            _stackFrames.Push(code);
            var tabs = Tabs();
            Console.WriteLine(tabs + "Executing: Line " + lineNumber);
            Console.WriteLine(tabs + "Statement: " + code);
            
            Console.ForegroundColor = ConsoleColor.White;
        }

        public static string PopFrame(string message = "", ConsoleColor color = ConsoleColor.Yellow)
        {
            if (message == null)
                return _stackFrames.Pop();

            Console.ForegroundColor = ConsoleColor.Yellow;
            var tabs = Tabs();
            Console.WriteLine(tabs + "Execution Complete");
            Console.ForegroundColor = ConsoleColor.White;
            return _stackFrames.Pop();
        }

        private static string Tabs()
        {
            var tabs = "";
            if (_stackFrames.Count > 1)
            {
                foreach (var f in _stackFrames.Skip(1))
                {
                    tabs += "\t";
                }
            }
            return tabs;
        }

        public static CodeContext Create(string name, Type runtimeType, Type modelType, string checksum)
        {
            if (!runtimeType.Implements<IRuntime>())
                throw new InvalidOperationException("The Runtime type must implement IRuntime");

            ResetStack();

            CodeContext context = null;
            lock (_contexts)
            {
                if (name == null || !_contexts.TryGetValue(name + modelType.FullName, out context) || !context.Checksum.Equals(checksum) || !context.IsCompleted)
                {
                    if (context != null)
                    {
                        _contexts.Remove(name + modelType.FullName);
                    }
                    context = new CodeContext(name, runtimeType, modelType, checksum);

                    if (name != null)
                        _contexts.Add(name + modelType.FullName, context);
                }
            }
            return context;
        }

        protected CodeContext(string name, Type runtimeType, Type modelType, string checksum)
        {
            Initialize(name, runtimeType, modelType, checksum);
        }

        protected virtual void Initialize(string name, Type runtimeType, Type modelType, string checksum)
        {
            if (!modelType.Implements<IModel>())
                throw new InvalidOperationException("The modelType must implement the IModel interface.");

            VariableScope.Clear();

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
            

            if (!VariableScope.Current.IsRoot)
                VariableScope.Push();
        }

        protected virtual void HandleError(string message, params string[] args)
        {
            throw new InvalidOperationException(string.Format(message, args));
        }

        protected virtual void PreProcess() { }

        protected bool TryGetParameter(string name, out Expression parameter)
        {
            parameter = null;

            if (name.StartsWith("@@root"))
            {
                parameter = VariableScope.Root[name];
                return parameter != null;
            }
            else if (name.StartsWith("@@parent"))
            {
                var split = name.Split('.');
                var index = 0;
                var scope = VariableScope.Current;
                for (int i = 0; i < split.Length; i++)
                {
                    if (split[i].Equals("@@parent"))
                    {
                        index++;
                        scope = scope.Parent;
                    }
                    else if ((split[i].Equals("@@index") || split[i].Equals("@@count")) && i == split.Length - 1)
                    {
                        parameter = scope[split[i]];
                        return parameter != null;
                    }
                }
            }

            parameter = VariableScope.Search(name);

            return parameter != null;
        }

        protected bool TryGetRuntimeProperty(string name, out Expression property)
        {
            var propertyInfo = this.RuntimeType.GetPublicProperties()
                .FirstOrDefault(pi => pi.Name.Equals(name, StringComparison.InvariantCultureIgnoreCase) || ("@" + pi.Name).Equals(name, StringComparison.InvariantCultureIgnoreCase));
            property = null;
            if (propertyInfo == null) return false;
            property = Expression.MakeMemberAccess(Expression.Convert(this.Runtime, this.RuntimeType), propertyInfo);

            if (name.Equals("@model"))
            {
                property = Convert(property, ModelType); // get the model into the proper type, not just IModel
            }

            return true;
        }

        public void AppendCode(string code)
        {
            IEnumerable<DDV.DDVError> errors;
            DDVParser.CodeStatementContext ctx = null;
            if (DDV.Validate(code, (p) => { ctx = p.codeStatement(); return ctx; }, out errors))
            {
                AppendCode(ctx);
            }
            else
            {
                throw new InvalidOperationException("The script could not be parsed.");
            }
        }

        public void AppendCode(DDVParser.CodeStatementContext codeStatementContext)
        {
            IsCodeBlock = true;
            Visit(codeStatementContext);
            IsCodeBlock = false;
        }

        protected string ErrorText(IEnumerable<DDV.DDVError> errors)
        {
            var sb = new StringBuilder();
            sb.Append("Parsing errors:");
            foreach (var error in errors)
            {
                sb.AppendFormat("Line: {0}, Character: {1} {2}", error.Line, error.Character, error.Error);
            }
            return sb.ToString();
        }

        public virtual void AppendFor(string iterator, Func<Expression> body)
        {
            Expression collection, initValue, increment, condition;
            ParameterExpression loopVar;
            using (var scope = new VariableScope())
            {
                if (TryParseForEachIterator(iterator, out collection, out loopVar))
                {
                    this.Statements.Add(ForEach(collection, loopVar, body));
                }
                else if (TryParseForIterator(iterator, out loopVar, out initValue, out condition, out increment))
                {
                    this.Statements.Add(For(loopVar, initValue, condition, increment, body));
                }
                else
                {
                    HandleError("Invalid For Iterator: {0}", iterator);
                }
            }
        }

        public virtual void AppendIf(string condition, Func<Expression> body)
        {
            Expression logicalExpression;
            PreProcess();
            if (TryParseIfCondition(condition, out logicalExpression))
            {
                var ifExp = Expression.IfThen(logicalExpression, body());
                this.Statements.Add(ifExp);
            }
            else
            {
                HandleError("Invalid Id Condition: {0}", condition);
            }
        }

        protected bool TryParseIfCondition(string condition, out Expression logicalExpression)
        {
            DDVParser.LogicalExpressionContext context = null;
            IEnumerable<DDV.DDVError> errors;
            logicalExpression = null;
            if (DDV.Validate(condition, (parser) => { context = parser.logicalExpression(); return context; }, out errors))
            {
                logicalExpression = VisitLogicalExpression(context);
                return true;
            }
            return false;
        }

        protected bool TryParseForEachIterator(string iterator, out Expression collection, out ParameterExpression loopVar)
        {
            DDVParser.ForEachIteratorContext context = null;
            IEnumerable<DDV.DDVError> errors;
            collection = null;
            loopVar = null;
            if (DDV.Validate(iterator, (parser) => { context = parser.forEachIterator(); return context; }, out errors))
            {
                VisitForEachIterator(context, out collection, out loopVar);
                return true;
            }
            return false;
        }

        protected bool TryParseForIterator(string iterator, out ParameterExpression loopVar, out Expression initValue, out Expression condition, out Expression increment)
        {
            DDVParser.ForIteratorContext context = null;
            IEnumerable<DDV.DDVError> errors;
            loopVar = null;
            initValue = null;
            condition = null;
            increment = null;
            if (DDV.Validate(iterator, (parser) => { context = parser.forIterator(); return context; }, out errors))
            {
                VisitForIterator(context, out loopVar, out initValue, out condition, out increment);
                return true;
            }
            return false;
        }



        protected virtual Expression ForEach(Expression collection, ParameterExpression loopVar, Func<Expression> body)
        {
            /* EXAMPLE USAGE
                var collection = Expression.Parameter(typeof(List<string>), "collection");
                var loopVar = Expression.Parameter(typeof(string), "loopVar");
                var loopBody = Expression.Call(typeof(Console).GetMethod("WriteLine", new[] { typeof(string) }), loopVar);
                var loop = ForEach(collection, loopVar, loopBody);
                var compiled = Expression.Lambda<Action<List<string>>>(loop, collection).Compile();
                compiled(new List<string>() { "a", "b", "c" });

                foreach(loopvar in collection)
                {
                    body
                }
            */

            var elementType = loopVar.Type;
            var enumerableType = typeof(IEnumerable<>).MakeGenericType(elementType);
            var enumeratorType = typeof(IEnumerator<>).MakeGenericType(elementType);

            var enumeratorVar = Expression.Variable(enumeratorType, "enumerator");

            if (collection is MemberExpression && ((MemberExpression)collection).Expression.Type.Equals(typeof(PropertyInvoker)))
            {
                collection = Expression.Call(null, typeof(Invoker).GetMethod("AsEnumerable").MakeGenericMethod(elementType), collection);
            }

            var getEnumeratorCall = Expression.Call(collection, enumerableType.GetMethod("GetEnumerator"));
            var enumeratorAssign = Expression.Assign(enumeratorVar, getEnumeratorCall);

            // The MoveNext method's actually on IEnumerator, not IEnumerator<T>
            var moveNextCall = Expression.Call(enumeratorVar, typeof(IEnumerator).GetMethod("MoveNext"));

            var breakLabel = Expression.Label("LoopBreak");

            // making @@index and @@count available in ForEach loops
            var indexVar = (ParameterExpression)CreateForEachIndex();
            var lengthVar = (ParameterExpression)CreateForEachCount(collection);
            var indexInit = Expression.Assign(indexVar, Expression.Constant(0));
            var lengthInit = Expression.Assign(
                lengthVar,
                Expression.Call(
                    null,
                    typeof(Enumerable).GetMethods(BindingFlags.Public | BindingFlags.Static)
                                        .Single(mi => mi.Name.Equals("Count") && mi.GetParameters().Length == 1)
                                        .MakeGenericMethod(elementType),
                    collection));
            var indexIncrement = Expression.Assign(indexVar, Expression.Add(indexVar, Expression.Constant(1)));

            var loop = CreateBlock(new[] { enumeratorVar, indexVar, lengthVar },
                enumeratorAssign,
                indexInit,
                lengthInit,
                Expression.Loop(
                    Expression.IfThenElse(
                        Expression.Equal(moveNextCall, Expression.Constant(true)),
                        CreateBlock(new[] { loopVar },
                            Expression.Assign(loopVar, Expression.Property(enumeratorVar, "Current")),
                            body(),
                            indexIncrement
                        ),
                        Expression.Break(breakLabel)
                    ),
                breakLabel));

            return loop;

        }

        protected virtual Expression Convert(Expression source, Type type)
        {
            if (source.Type.Equals(type)) return source;
            //Debug.WriteLine("Source: {0}  Target: {1} Expression: {2}", source.Type.Name, type.Name, source.ToString());
            if (source is MemberExpression && ((MemberExpression)source).Expression.Type.Equals(typeof(PropertyInvoker)))
            {
                return Expression.Call(null,
                                        typeof(Invoker).GetMethods(BindingFlags.Public | BindingFlags.Static)
                                                        .Single(mi => mi.Name.Equals("Cast") && mi.GetParameters()[0].ParameterType.Equals(typeof(object)))
                                                        .MakeGenericMethod(type),
                                        source);
            }
            else
            {
                if (type == typeof(string))
                {
                    var toString = source.Type.GetMethods(BindingFlags.Public | BindingFlags.Instance).Single(mi => mi.Name.Equals("ToString") && mi.GetParameters().Length == 0);
                    return Expression.Call(source, toString);
                }
                else
                {
                    return Expression.Convert(source, type);
                }
            }
        }

        protected virtual BlockExpression CreateBlock(ParameterExpression[] parameterExpressions, params Expression[] statements)
        {
            //var parameters = parameterExpressions.Union(VariableFinder.FindParameters(statements))
            //                                     .Except(new ParameterExpression[] { this.IRuntime }).OfType<ParameterExpression>()
            //                                     .ToArray();
            return Expression.Block(parameterExpressions, statements.Where(e => e != null));
        }

        protected virtual Expression ForEach(Expression collection, ParameterExpression loopVar, Expression body)
        {
            /* EXAMPLE USAGE
                var collection = Expression.Parameter(typeof(List<string>), "collection");
                var loopVar = Expression.Parameter(typeof(string), "loopVar");
                var loopBody = Expression.Call(typeof(Console).GetMethod("WriteLine", new[] { typeof(string) }), loopVar);
                var loop = ForEach(collection, loopVar, loopBody);
                var compiled = Expression.Lambda<Action<List<string>>>(loop, collection).Compile();
                compiled(new List<string>() { "a", "b", "c" });

                foreach(loopvar in collection)
                {
                    body
                }
            */

            var elementType = loopVar.Type;
            var enumerableType = typeof(IEnumerable<>).MakeGenericType(elementType);
            var enumeratorType = typeof(IEnumerator<>).MakeGenericType(elementType);

            var enumeratorVar = Expression.Variable(enumeratorType, "enumerator");

            if (collection is MemberExpression && ((MemberExpression)collection).Expression.Type.Equals(typeof(PropertyInvoker)))
            {
                collection = Expression.Call(null, typeof(Invoker).GetMethod("AsEnumerable"), collection);
            }

            var getEnumeratorCall = Expression.Call(collection, enumerableType.GetMethod("GetEnumerator"));
            var enumeratorAssign = Expression.Assign(enumeratorVar, getEnumeratorCall);

            // The MoveNext method's actually on IEnumerator, not IEnumerator<T>
            var moveNextCall = Expression.Call(enumeratorVar, typeof(IEnumerator).GetMethod("MoveNext"));

            var breakLabel = Expression.Label("LoopBreak");

            var loop = CreateBlock(new[] { enumeratorVar },
                enumeratorAssign,
                Expression.Loop(
                    Expression.IfThenElse(
                        Expression.Equal(moveNextCall, Expression.Constant(true)),
                        CreateBlock(new[] { loopVar },
                            Expression.Assign(loopVar, Expression.Property(enumeratorVar, "Current")),
                            body
                        ),
                        Expression.Break(breakLabel)
                    ),
                breakLabel)
            );

            return loop;
        }

        protected virtual Expression For(ParameterExpression loopVar, Expression initValue, Expression condition, Expression increment, Expression body)
        {
            /* EXAMPLE
                for (loopVar = initValue; condition; increment)
                {
                    body
                }
            */

            var initAssign = Expression.Assign(loopVar, initValue);

            var breakLabel = Expression.Label("LoopBreak");

            var loop = CreateBlock(new[] { loopVar },
                initAssign,
                Expression.Loop(
                    Expression.IfThenElse(
                        condition,
                        CreateBlock(new ParameterExpression[0],
                            body,
                            increment
                        ),
                        Expression.Break(breakLabel)
                    ),
                breakLabel)
            );

            return loop;
        }

        protected virtual Expression For(ParameterExpression loopVar, Expression initValue, Expression condition, Expression increment, Func<Expression> body)
        {
            /* EXAMPLE
                for (loopVar = initValue; condition; increment)
                {
                    body
                }
            */

            var initAssign = Expression.Assign(loopVar, initValue);

            var breakLabel = Expression.Label("LoopBreak");

            var loop = CreateBlock(new[] { loopVar },
                initAssign,
                Expression.Loop(
                    Expression.IfThenElse(
                        condition,
                        CreateBlock(new ParameterExpression[0],
                            body(),
                            increment
                        ),
                        Expression.Break(breakLabel)
                    ),
                breakLabel)
            );

            return loop;
        }

        protected BlockExpression ReBlock(params Expression[] expressions)
        {
            var statements = new List<Expression>();
            var parameters = new List<ParameterExpression>();

            foreach (var exp in expressions)
            {
                if (exp is BlockExpression)
                {
                    IEnumerable<Expression> blkExps;
                    IEnumerable<ParameterExpression> blkParms;
                    BreakBlock(exp as BlockExpression, out blkParms, out blkExps);
                    statements.AddRange(blkExps);
                    parameters.AddRange(blkParms);
                }
                else
                {
                    statements.Add(exp);
                }
            }
            return CreateBlock(parameters.ToArray(), statements.ToArray());
        }

        protected void BreakBlock(BlockExpression blockExpression, out IEnumerable<ParameterExpression> blkParms, out IEnumerable<Expression> blkExps)
        {
            blkParms = blockExpression.Variables;
            blkExps = blockExpression.Expressions;
        }

        public virtual Expression CompleteScope(bool compile = false)
        {
            if (!IsCompleted)
            {
                // compile the function

                if (compile)
                {
                    try
                    {
                        PreProcess();
                        IEnumerable<ParameterExpression> blkParms;
                        IEnumerable<Expression> blkExps;

                        // we need to add some statements to the end of the sub scope block in order to return the compiled text
                        // so we need to reconstitute the original expressions from the sub scope block
                        BreakBlock(SubScope() as BlockExpression, out blkParms, out blkExps);
                        this.Statements.Clear();
                        this.Statements.AddRange(blkExps);

                        // make sure we return void
                        var returnLabel = Expression.Label(typeof(void), "return");
                        this.Statements.Add(Expression.Return(returnLabel));
                        this.Statements.Add(Expression.Label(returnLabel));

                        var parms = new List<ParameterExpression>();
                        parms.AddRange(VariableFinder.FindParameters(this.Statements));
                        

                        var scope = CreateBlock(
                                            parms.ToArray(),
                                            this.Statements.ToArray());

                        var exceptionRef = Expression.Variable(typeof(Exception), "ex");
                        var exToString = Expression.Call(exceptionRef, typeof(object).GetMethod("ToString"));

                        this.Statements.Clear();
                        this.Statements.Add(scope);

                        var delegateType = typeof(Action<>).MakeGenericType(this.IRuntime.Type);

                        var pushFrame = Expression.Call(null,
                                                        typeof(CodeContext).GetMethod("PushFrame", BindingFlags.Public | BindingFlags.Static),
                                                        exToString,
                                                        Expression.Constant(0),
                                                        Expression.Constant(ConsoleColor.Red));
                        var popFrame = Expression.Call(null,
                                                       typeof(CodeContext).GetMethod("PopFrame", BindingFlags.Public | BindingFlags.Static),
                                                       Expression.Constant(null, typeof(string)),
                                                       Expression.Constant(ConsoleColor.Red));
                        var throwError = Expression.Throw(exceptionRef);
                        
                        var emitError = Expression.Block(pushFrame, popFrame, throwError);

                        var tryCatch = Expression.TryCatch(
                            scope,
                            Expression.Catch(exceptionRef,
                                emitError));

                        this.CompletedScopeExpression = Expression.Lambda(delegateType, tryCatch, this.IRuntime);
                        this.CompletedScope = ((LambdaExpression)CompletedScopeExpression).Compile();

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

        public virtual Expression SubScope()
        {
            if (IsSubscoped)
            {
                return this.Statements[0];
            }
            else
            {
                PreProcess();

                var parms = new List<ParameterExpression>();
                parms.AddRange(VariableFinder.FindParameters(this.Statements));

                var scope = CreateBlock(
                                    parms.ToArray(),
                                    this.Statements.ToArray());

                this.Statements.Clear();
                this.Statements.Add(scope);

                IsSubscoped = true;
                return this.Statements[0];
            }
        }
        public string Checksum { get; protected set; }
        public string Name { get; protected set; }
        public bool IsCompleted { get; protected set; }
        public bool IsSubscoped { get; protected set; }
        public List<Expression> Statements { get; protected set; }
        public MethodInfo AppendMethod { get; protected set; }
        public ParameterExpression IRuntime { get; protected set; }
        public Expression Runtime { get; protected set; }
        public Delegate CompletedScope { get; protected set; }
        public Expression CompletedScopeExpression { get; protected set; }
        public Type RuntimeType { get; protected set; }
        public Type ModelType { get; protected set; }
        public bool IsCodeBlock { get; protected set; }
        public bool HasErrors { get; private set; }

        public virtual Expression Visit(IParseTree context)
        {
            try
            {
                if (context == null) return null;

                if (context is DDVParser.PropertyAccessContext)
                {
                    return VisitPropertyAccess(context as DDVParser.PropertyAccessContext);
                }
                else if (context is DDVParser.ParameterContext)
                {
                    return VisitParameter(context as DDVParser.ParameterContext);
                }
                else if (context is DDVParser.VariableContext)
                {
                    return VisitVariable(context as DDVParser.VariableContext);
                }
                else if (context is DDVParser.LambdaVariableContext)
                {
                    return VisitLambdaVariable(context as DDVParser.LambdaVariableContext);
                }
                else if (context is DDVParser.VariableDeclarationContext)
                {
                    return VisitVariableDeclaration(context as DDVParser.VariableDeclarationContext);
                }
                else if (context is DDVParser.ScopedVariableContext)
                {
                    return VisitScopedVariable(context as DDVParser.ScopedVariableContext);
                }
                else if (context is DDVParser.ArrayOfLiteralsContext)
                {
                    return VisitArrayOfLiterals(context as DDVParser.ArrayOfLiteralsContext);
                }
                else if (context is DDVParser.MethodAccessContext)
                {
                    return VisitMethodAccess(context as DDVParser.MethodAccessContext);
                }
                else if (context is DDVParser.LogicalExpressionContext)
                {
                    return VisitLogicalExpression(context as DDVParser.LogicalExpressionContext);
                }
                else if (context is DDVParser.BooleanLiteralContext)
                {
                    return VisitBooleanLiteral(context as DDVParser.BooleanLiteralContext);
                }
                else if (context is DDVParser.BinaryExpressionContext)
                {
                    return VisitBinaryExpression(context as DDVParser.BinaryExpressionContext);
                }
                else if (context is DDVParser.FunctionContext)
                {
                    return VisitFunction(context as DDVParser.FunctionContext);
                }
                else if (context is DDVParser.StringLiteralContext)
                {
                    return VisitStringLiteral(context as DDVParser.StringLiteralContext);
                }
                else if (context is DDVParser.FunctionExpressionContext)
                {
                    return VisitFunctionExpression(context as DDVParser.FunctionExpressionContext);
                }
                else if (context is DDVParser.ForLoopExpressionContext)
                {
                    return VisitForLoopExpression(context as DDVParser.ForLoopExpressionContext);
                }
                else if (context is DDVParser.FunctionExpressionContext)
                {
                    return VisitFunctionExpression(context as DDVParser.FunctionExpressionContext);
                }
                else if (context is DDVParser.PropertyAccessContext)
                {
                    return VisitPropertyAccess(context as DDVParser.PropertyAccessContext);
                }
                else if (context is DDVParser.LiteralContext)
                {
                    return VisitLiteral(context as DDVParser.LiteralContext);
                }
                else if (context is DDVParser.ArithmeticExpressionContext)
                {
                    return VisitArithmetic(context as DDVParser.ArithmeticExpressionContext);
                }
                else if (context is DDVParser.IntegerLiteralContext)
                {
                    return VisitInteger(context as DDVParser.IntegerLiteralContext);
                }
                else if (context is DDVParser.FloatLiteralContext)
                {
                    return VisitFloat(context as DDVParser.FloatLiteralContext);
                }
                else if (context is DDVParser.NullLiteralContext)
                {
                    return VisitNull(context as DDVParser.NullLiteralContext);
                }
                else if (context is DDVParser.NumberLiteralContext)
                {
                    return VisitNumber(context as DDVParser.NumberLiteralContext);
                }
                else if (context is DDVParser.ArithmeticOperatorContext)
                {
                    return VisitArithemticOperator(context as DDVParser.ArithmeticOperatorContext);
                }
                else if (context is DDVParser.ArrayOfLiteralsContext)
                {
                    return VisitArrayOfLiterals(context as DDVParser.ArrayOfLiteralsContext);
                }
                else if (context is DDVParser.AssignmentExpressionContext)
                {
                    return VisitAssignment(context.GetChild(0) as DDVParser.AssignmentContext);
                }
                else if (context is DDVParser.AssignmentContext)
                {
                    return VisitAssignment(context as DDVParser.AssignmentContext);
                }
                else if (context is DDVParser.TernaryFunctionContext)
                {
                    return VisitTernaryFunction(context as DDVParser.TernaryFunctionContext);
                }
                else if (context is DDVParser.InlineStatementContext)
                {
                    return VisitInlineStatement(context as DDVParser.InlineStatementContext);
                }
                else if (context is DDVParser.CodeStatementContext)
                {
                    this.Statements.Add(Expression.Call(null, typeof(CodeContext).GetMethod("ResetStack", BindingFlags.Public | BindingFlags.Static)));
                    var expressions = new List<Expression>();
                    var start = context.ChildCount > 1 && context.GetChild(0).GetText() == "<ddv>" ? 1 : 0;
                    var end = start > 0 ? 1 : 0;
                    for (int i = start; i < context.ChildCount - end; i++)
                    {
                        var exp = Visit(context.GetChild(i));
                        if (exp != null)
                            expressions.Add(exp);
                    }
                    //return CreateBlock(new ParameterExpression[0], expressions.ToArray());
                    foreach (var exp in expressions)
                    {
                        this.Statements.Add(exp);
                    }
                    return null;
                }
                else if (context is DDVParser.ObjectExpressionContext)
                {
                    return VisitObjectExpression(context as DDVParser.ObjectExpressionContext);
                }
                else if (context is DDVParser.FunctionDefinitionContext)
                {
                    return VisitFunctionDefinitionContext(context as DDVParser.FunctionDefinitionContext);
                }
                else if (context is DDVParser.ReturnExpressionContext)
                {
                    return VisitReturnExpression(context as DDVParser.ReturnExpressionContext);
                }
                else if (context is DDVParser.MemberExpressionContext)
                {
                    return VisitMemberAccess(context as DDVParser.MemberExpressionContext);
                }
                else if (context is DDVParser.IfExpressionContext)
                {
                    Expression condition, body;
                    return VisitIfExpression(context as DDVParser.IfExpressionContext, out condition, out body);
                }
                else if (context is Antlr4.Runtime.Tree.TerminalNodeImpl)
                {
                    return null;
                }
                throw new NotSupportedException(string.Format("The parse tree type '{0}' provided is not supported.", context.GetType().Name));
            }
            catch(Exception ex)
            {
                if (!HasErrors)
                {
                    HasErrors = true;
                    PushFrame("Script Compilation Error: " + ex.Message, 0, ConsoleColor.Red);
                    PushFrame(context.GetText(), (context as ParserRuleContext).Start.Line, ConsoleColor.Red);
                    PopFrame(null);
                    PopFrame(null);
                }
                throw ex;
            }
        }

        protected virtual Expression VisitIfExpression(IParseTree ctx, out Expression condition, out Expression body)
        {
            /* 
                ifElseExpression
	                :	Else ifExpression
	                ;

                elseExpression
	                :	Else BraceOpen (assignmentExpression | functionExpression | memberExpression)+ BraceClose
	                ;

                ifExpression
	                :	If OpenParen binaryExpression CloseParen BraceOpen (assignmentExpression | functionExpression | memberExpression)+ BraceClose ifElseExpression* elseExpression?
	                ;
            */

            var offset = 0;
            if (ctx.GetChild(0).GetText() == "else")
                offset = 1;

            condition = Visit(ctx.GetChild(2 + offset));
            var index = 5 + offset;
            IParseTree pt = ctx.GetChild(index);
            var statements = new List<IParseTree>();
            do
            {
                statements.Add(pt);
                index++;
                pt = ctx.GetChild(index);
            } while (pt.GetText() != "}");
            index++;
            body = VisitBodyBlock(statements);

            var otherConditions = new List<Expression>();
            var otherTrueBlocks = new List<Expression>();

            if (index < ctx.ChildCount)
            {
                while (index < ctx.ChildCount)
                {
                    Expression elseCondition, elseTrueBlock;
                    VisitIfElse(ctx.GetChild(index), out elseCondition, out elseTrueBlock);
                    otherConditions.Add(elseCondition);
                    otherTrueBlocks.Add(elseTrueBlock);
                    index++;
                }
            }

            if (otherConditions.Count > 0)
            {
                otherConditions.Reverse();
                otherTrueBlocks.Reverse();
                Expression ifChain = null;
                for (int i = 0; i < otherConditions.Count; i++)
                {
                    if (i == 0)
                    {
                        ifChain = Expression.IfThen(otherConditions[i], otherTrueBlocks[i]);
                    }
                    else
                    {
                        ifChain = Expression.IfThenElse(otherConditions[i], otherTrueBlocks[i], ifChain);
                    }
                }
                ifChain = Expression.IfThenElse(condition, body, ifChain);
                return ifChain;
            }
            else
            {
                return Expression.IfThen(condition, body);
            }

        }

        protected void VisitIfElse(IParseTree ctx, out Expression elseCondition, out Expression elseTrueBlock)
        {
            /*
             
                ifElseExpression
	                :	Else ifExpression
	                ;

                elseExpression
	                :	Else BraceOpen (assignmentExpression | functionExpression | memberExpression)+ BraceClose
	                ;
             
            */
            if (ctx is DDVParser.IfElseExpressionContext)
            {
                VisitIfExpression(ctx, out elseCondition, out elseTrueBlock);
            }
            else if (ctx is DDVParser.ElseExpressionContext)
            {
                elseCondition = Expression.Constant(true);
                elseTrueBlock = VisitBodyBlock(((DDVParser.ElseExpressionContext)ctx).children.Skip(2).Take(ctx.ChildCount - 3));
            }
            else
            {
                throw new NotSupportedException("The parsing context type is not supported for IfElse or Else expressions.");
            }
        }

        protected virtual Expression VisitMemberAccess(DDVParser.MemberExpressionContext ctx)
        {
            /* 
                memberExpression
	            :	(propertyAccess | methodAccess) semiColon
	            ;
            */
            var member = Visit(ctx.GetChild(0));
            var pushFrame = Expression.Call(null,
                                            typeof(CodeContext).GetMethod("PushFrame", BindingFlags.Public | BindingFlags.Static),
                                            Expression.Constant(ctx.GetText()),
                                            Expression.Constant(ctx.Start.Line),
                                            Expression.Constant(ConsoleColor.Yellow));
            var popFrame = Expression.Call(null,
                                           typeof(CodeContext).GetMethod("PopFrame", BindingFlags.Public | BindingFlags.Static),
                                           Expression.Constant(null, typeof(string)),
                                           Expression.Constant(ConsoleColor.Yellow));
            var block = CreateBlock(new ParameterExpression[0], new Expression[] { pushFrame, member });
            return Expression.TryCatchFinally(block, popFrame);
        }

        protected virtual Expression VisitReturnExpression(DDVParser.ReturnExpressionContext ctx)
        {
            /* 
                returnExpression
	            :	Return parameter? SemiColon
	            ; 
                parameter
	            :	literal | propertyAccess | methodAccess | variable | lambdaVariable | arrayOfLiterals 
	            |	arithmeticExpression | binaryExpression | function | ternaryFunction | objectExpression | functionDefinition
	            ;

            */

            if (ctx.ChildCount == 3)
            {
                // we have a return value
                return Visit(ctx.GetChild(1));
            }
            else
            {
                // there's no return value
                return null;
            }
        }

        static Dictionary<Type, DDVParser.ObjectExpressionContext> _objectDefs = new Dictionary<Type, DDVParser.ObjectExpressionContext>();
        protected virtual Expression VisitObjectExpression(DDVParser.ObjectExpressionContext ctx, Type cachedtype = null)
        {
            /* 
             objectExpression
	            :	BraceOpen namedElement Colon parameter (Comma namedElement Colon parameter)* BraceClose
	            ; 
            */
            var members = new Dictionary<string, IParseTree>();
            for (int i = 1; i < ctx.ChildCount; i += 4)
            {
                var name = ctx.GetChild(i).GetText();
                var value = ctx.GetChild(i + 2);
                members.Add(name, value);
            }

            // if props include ModelType property, we need to scope assignment expression by known property types
            // otherwise, just use defaults
            var typeCtx = members.FirstOrDefault(kvp => kvp.Key == "@@type");
            var @ThisMembers = new Dictionary<string, Expression>();
            var @this = Expression.Variable(typeof(object), "@@this");
            Type type = null;
            string typeName = null;
            using (var thisScope = new VariableScope())
            {
                if (typeCtx.Key != null)
                {
                    typeName = ((ConstantExpression)Visit(typeCtx.Value)).Value.ToString();
                    if (ModelTypeManager.TryGetModelType(typeName, out type) || AnonymousTypeBuilder.TryGetType(typeName, out type))
                    {
                        thisScope.Add(@this.Name, @this);
                        var props = type.GetPublicProperties();

                        foreach (var kvp in members.Where(kvp => kvp.Key != "@@type"))
                        {
                            try
                            {
                                var prop = props.Single(p => p.Name.Equals(kvp.Key));
                                using (var scope = new ExpressionScope(prop.PropertyType, prop.Name, null, true))
                                {
                                    @ThisMembers.Add(prop.Name, Visit(kvp.Value));
                                }
                            }
                            catch (InvalidOperationException ex)
                            {
                                if (ex.Message.StartsWith("Sequence"))
                                {
                                    throw new InvalidOperationException(string.Format("A property named '{0}' on Model Type '{1}' could not be found.", kvp.Key, typeName));
                                }
                            }
                        }
                    }
                }

                if (type == null)
                {
                    if (cachedtype == null)
                    {
                        thisScope.Add(@this.Name, @this);
                        // we need to figure out what data types the members will be, which can create dependency issues
                        // where inline functions reference @@this.Property values, when the type has not yet been created
                        // so we try to build the member expressions in two passes - the first pass will determine the member types
                        // and then the second pass will actually reference the constructed type for lambda member references
                        foreach (var kvp in members)
                        {
                            using (var scope = new ExpressionScope(typeof(object), kvp.Key, null, true, @ThisMembers))
                            {
                                @ThisMembers.Add(kvp.Key, Visit(kvp.Value));
                            }
                        }

                        type = AnonymousTypeBuilder.CreateType(@ThisMembers);
                        thisScope.Remove("@@this");
                    }
                    else
                    {
                        type = cachedtype;
                    }
                    
                    @this = Expression.Variable(type, "@@this");
                    thisScope.Add(@this.Name, @this);
                    @ThisMembers.Clear();

                    var memberDefs = new Dictionary<string, Expression>();
                    foreach (var kvp in members.Where(kvp => kvp.Key != "@@type"))
                    {
                        using (var scope = new ExpressionScope(type.GetProperty(kvp.Key).PropertyType, kvp.Key, @this, true))
                        {
                            memberDefs.Add(kvp.Key, Visit(kvp.Value));
                        }
                    }

                    @ThisMembers = memberDefs;
                }
            }

            type = AnonymousTypeBuilder.CreateType(@ThisMembers, typeName);
            DDVParser.ObjectExpressionContext cached = null;
            lock(_objectDefs)
            {
                _objectDefs.TryGetValue(type, out cached);
            }
            // for objects with funcs, this will build a vanilla instance from the first instance
            var newExp = (cached == null || cachedtype != null) ? Expression.New(type.GetConstructor(Type.EmptyTypes)) : VisitObjectExpression(cached, type); 
            var statements = new List<Expression>();
            using (var scope = new VariableScope())
            {
                var obj = Expression.Variable(type, "@obj");
                
                VariableScope.Current.Add(obj.Name, obj);
                var returnLabel = Expression.Label(type, "return");
                statements.Add(Expression.Assign(obj, newExp));
                if (type.Implements<IModel>())
                {
                    var pubs = type.GetPublicProperties();
                    var props = pubs.Where(p => p.CanWrite && @ThisMembers.Any(m => m.Key.Equals(p.Name))).ToArray();
                    if (!members.All(m => pubs.Any(p => p.Name.Equals(m.Key))))
                        throw new InvalidOperationException("Properties designated in script do not exist on the model.");

                    var assignments = props.Select(p => new { Property = p, Entry = @ThisMembers.First(m => m.Key.Equals(p.Name)) })
                                            .Select(a => Expression.Assign(Expression.Property(obj, a.Property), a.Entry.Value));
                    statements.AddRange(assignments);
                }
                else
                {
                    statements.AddRange(@ThisMembers.Select(kvp => Expression.Assign(Expression.Field(obj, type.GetField("_" + kvp.Key)), kvp.Value)));
                }
                statements.Add(Expression.Return(returnLabel, obj));
                statements.Add(Expression.Label(returnLabel, Expression.Constant(null, type)));
                var newObj = CreateBlock(new ParameterExpression[] { obj }, statements.ToArray());
                newObj = (BlockExpression)VariableSubstitutor.Replace(newObj, @this, obj);
                lock(_objectDefs)
                {
                    if (!_objectDefs.ContainsKey(type))
                    {
                        _objectDefs.Add(type, ctx);
                    }
                }
                return newObj;
            }

        }

        protected virtual Expression VisitLambdaVariable(DDVParser.LambdaVariableContext ctx)
        {
            //lambdaVariable
            //    : decimalLiteral Dollar
            //    ;

            var variableName = ctx.GetText();
            var variable = VariableScope.Search(variableName);
            if (variable != null)
            {
                return variable;
            }
            var scope = ExpressionScope.Current;
            if (scope == null)
                throw new InvalidOperationException(string.Format("The variable type for '{0}' could not be determined.", variableName));
            variable = Expression.Variable(scope.InstanceType, variableName);
            if (IsCodeBlock)
            {
                VariableScope.Root.Add(variableName, variable);
            }
            else
            {
                VariableScope.Current.Add(variableName, variable);
            }
            return variable;
        }

        protected virtual Expression VisitParameter(DDVParser.ParameterContext ctx)
        {
            return Visit(ctx.GetChild(0));
        }

        protected virtual Expression VisitScopedVariable(DDVParser.ScopedVariableContext ctx)
        {
            //scopedVariable
            //    :	At At namedElement
            //    ;

            var variableName = ctx.GetText();
            Expression variable = null;
            if (!TryGetParameter(variableName, out variable))
            {
                throw new InvalidOperationException("The variable specified '" + variableName + "' could not be found.");
            }
            return variable;
        }

        protected virtual Expression VisitInlineStatement(DDVParser.InlineStatementContext ctx)
        {
            /* 
                inlineStatement
	                :	('[[') (variable | scopedVariable | function | propertyAccess | methodAccess | ternaryFunction | literal | scopedVariable) (']]')
	                |	(variable | scopedVariable | function | propertyAccess | methodAccess | ternaryFunction | literal | scopedVariable)
	                ;
            */

            if (ctx.ChildCount == 1)
            {
                return Visit(ctx.GetChild(0));
            }
            else
            {
                return Visit(ctx.GetChild(1));
            }
        }

        protected virtual Expression VisitTernaryFunction(DDVParser.TernaryFunctionContext ctx)
        {
            /* 
             ternaryFunction
	            :	(function | literal | propertyAccess | methodAccess | arithmeticExpression | variable  | lambdaVariable) booleanOperator (function | literal | propertyAccess | methodAccess | arithmeticExpression | ternaryFunction | variable | lambdaVariable) 
				            '?' (variable | lambdaVariable | scopedVariable | function | propertyAccess | methodAccess | ternaryFunction | literal | scopedVariable) 
				            ':' (variable | lambdaVariable | scopedVariable | function | propertyAccess | methodAccess | ternaryFunction | literal | scopedVariable)
	            |	function 
				            '?' (variable | lambdaVariable | scopedVariable | function | propertyAccess | methodAccess | ternaryFunction | literal | scopedVariable) 
				            ':' (variable | lambdaVariable | scopedVariable | function | propertyAccess | methodAccess | ternaryFunction | literal | scopedVariable)
	            |	propertyAccess 
				            '?' (variable | lambdaVariable | scopedVariable | function | propertyAccess | methodAccess | ternaryFunction | literal | scopedVariable) 
				            ':' (variable | lambdaVariable | scopedVariable | function | propertyAccess | methodAccess | ternaryFunction | literal | scopedVariable)
	            |	booleanLiteral 
				            '?' (variable | lambdaVariable | scopedVariable | function | propertyAccess | methodAccess | ternaryFunction | literal | scopedVariable) 
				            ':' (variable | lambdaVariable | scopedVariable | function | propertyAccess | methodAccess | ternaryFunction | literal | scopedVariable)
	            |	ternaryFunction 
				            '?' (variable | lambdaVariable | scopedVariable | function | propertyAccess | methodAccess | ternaryFunction | literal | scopedVariable) 
				            ':' (variable | lambdaVariable | scopedVariable | function | propertyAccess | methodAccess | ternaryFunction | literal | scopedVariable)
	            ;
            */
            if (ctx.ChildCount == 5)
                return Expression.Condition(Visit(ctx.GetChild(0)), Visit(ctx.GetChild(2)), Visit(ctx.GetChild(4)));
            else
            {
                var condition = VisitBinaryExpression(ctx); // first piece matches Binary Expression structure, so reuse that
                return Expression.Condition(condition, Visit(ctx.GetChild(4)), Visit(ctx.GetChild(6)));
            }
        }

        protected virtual Expression VisitAssignment(DDVParser.AssignmentContext ctx)
        {
            /* assignment
	            :	(variable | variableDeclaration | propertyAccess) assignmentOperator (literal | propertyAccess | methodAccess | variable | arrayOfLiterals | arithmeticExpression | binaryExpression | function | logicalExpression)
	            |   variable incrementOperator
	            ;
            */

            var variableContext = ctx.GetChild(0);
            Expression variable;
            Expression operand;

            if (variableContext is DDVParser.VariableContext)
            {
                variable = Visit(variableContext);
                if (ctx.ChildCount == 2)
                {
                    // simple incrementor
                    object cast;
                    ValueTypesEx.TryCast(1, variable.Type, out cast);
                    operand = Expression.Constant(cast, variable.Type);
                }
                else
                {
                    // binary assignment expression
                    using (var scope = new ExpressionScope(variable.Type, variableContext.GetText(), variable, true))
                    {
                        operand = Visit(ctx.GetChild(2));
                    }
                }
            }
            else
            {
                // the new variable will take its type from the assigned value's type, so we need to compute that expression first
                if (ctx.ChildCount == 2)
                {
                    // simple incrementor
                    operand = Expression.Constant(1, typeof(long));
                }
                else
                {
                    // binary assignment expression
                    operand = Visit(ctx.GetChild(2));
                }
                using (var scope = new ExpressionScope(operand.Type, variableContext.GetText(), operand, true))
                {
                    variable = Visit(variableContext);
                }
            }

            if (!operand.Type.Equals(variable.Type))
                operand = Convert(operand, variable.Type);
            Expression assign = null;
            if (ctx.ChildCount == 2)
            {
                // simple incrementor
                /* 
                    IncrementOperator
                    :	'++'
                    |	'--'
                    ; 
                */

                switch (ctx.GetChild(1).GetText())
                {
                    case "++":
                        {
                            assign = Expression.Assign(variable, Expression.Add(variable, operand));
                            break;
                        }
                    case "--":
                        {
                            assign = Expression.Assign(variable, Expression.Subtract(variable, operand));
                            break;
                        }
                }
            }
            else
            {
                // binary assignment expression
                /* 
                AssignmentOperator
                    :	'='
                    |	'+='
                    |	'-='
                    |	'*='
                    |	'\\='
                    ; 
                */
               
                switch (ctx.GetChild(1).GetText())
                {
                    case "=":
                        {
                            assign = Expression.Assign(variable, operand);
                            break;
                        }
                    case "+=":
                        {
                            if (variable.Type.IsNumeric())
                                assign = Expression.Assign(variable, Expression.Add(variable, operand));
                            else if (variable.Type.Equals(typeof(string)))
                            {
                                var concatMethod = typeof(string).GetMethods().Single(mi => mi.GetParameters().Length == 1 && mi.GetParameters()[0].ParameterType.Equals(typeof(string[])));
                                var concatVals = Expression.NewArrayInit(typeof(string), variable, operand);
                                var concat = Expression.Call(null, concatMethod, concatVals);
                                assign = concat;
                            }
                            else
                                throw new NotSupportedException();
                            break;
                        }
                    case "-=":
                        {
                            if (variable.Type.IsNumeric())
                                assign = Expression.Assign(variable, Expression.Subtract(variable, operand));
                            else
                                throw new NotSupportedException();
                            break;
                        }
                    case "*=":
                        {
                            if (variable.Type.IsNumeric())
                                assign = Expression.Assign(variable, Expression.Multiply(variable, operand));
                            else
                                throw new NotSupportedException();
                            break;
                        }
                    case "\\=":
                        {
                            if (variable.Type.IsNumeric())
                                assign = Expression.Assign(variable, Expression.Divide(variable, operand));
                            else
                                throw new NotSupportedException();
                            break;
                        }
                }
            }

            if (assign == null)
                throw new NotSupportedException(string.Format("The assignment expression '{0}' is not supported.", ctx.GetText()));

            var pushFrame = Expression.Call(null,
                                            typeof(CodeContext).GetMethod("PushFrame", BindingFlags.Public | BindingFlags.Static),
                                            Expression.Constant(ctx.GetText()),
                                            Expression.Constant(ctx.Start.Line),
                                            Expression.Constant(ConsoleColor.Yellow));
            var popFrame = Expression.Call(null,
                                           typeof(CodeContext).GetMethod("PopFrame", BindingFlags.Public | BindingFlags.Static),
                                           Expression.Constant(null, typeof(string)),
                                           Expression.Constant(ConsoleColor.Yellow));
            var block = CreateBlock(new ParameterExpression[0], new Expression[] { pushFrame, assign });
            return Expression.TryCatchFinally(block, popFrame);
        }


        protected virtual Expression VisitForLoopExpression(DDVParser.ForLoopExpressionContext ctx)
        {
            /* 
            forLoopExpression
	        :	ForEach OpenParen forEachIterator CloseParen BraceOpen (assignmentExpression | functionExpression | memberExpression)* BraceClose
	        |   For OpenParen forIterator CloseParen BraceOpen (assignmentExpression | functionExpression | memberExpression)* BraceClose
	        ; 
            */
            var forCtx = ctx.GetChild(0);
            using (var scope = new VariableScope())
            {
                if (forCtx.GetText().Equals("for", StringComparison.InvariantCultureIgnoreCase))
                {
                    // for loop
                    return VisitForLoop(ctx);
                }
                else
                {
                    // for each loop
                    return VisitForEachLoop(ctx);
                }
            }
        }

        protected virtual Expression VisitForEachLoop(DDVParser.ForLoopExpressionContext ctx)
        {
            Expression collection;
            ParameterExpression loopVar;
            VisitForEachIterator(ctx.GetChild(2) as DDVParser.ForEachIteratorContext, out collection, out loopVar);
            var body = VisitBodyBlock(ctx.children.Skip(5).Take(ctx.ChildCount - 6));

            return ForEach(collection, loopVar, body);
        }

        protected virtual Expression CreateForEachCount(Expression collection)
        {
            var lengthVar = Expression.Variable(typeof(int), "@@count");
            VariableScope.Current.Add(lengthVar.Name, lengthVar);
            var countMethod = typeof(Enumerable).GetMethods(BindingFlags.Public | BindingFlags.Static)
                                                .Single(mi => mi.Name.Equals("Count") && mi.GetParameters().Length == 1)
                                                .MakeGenericMethod(collection.Type.ResolveElementType());
            return lengthVar;

        }

        protected virtual Expression CreateForEachIndex()
        {
            var indexVar = Expression.Variable(typeof(int), "@@index");
            VariableScope.Current.Add(indexVar.Name, indexVar);
            return indexVar;
        }

        protected virtual Expression VisitBodyBlock(IEnumerable<IParseTree> statements)
        {
            var expressions = new List<Expression>();
            foreach (var s in statements)
            {
                expressions.Add(Visit(s));
            }
            return CreateBlock(new ParameterExpression[0], expressions.ToArray());
        }

        protected virtual Expression VisitForLoop(DDVParser.ForLoopExpressionContext ctx)
        {
            ParameterExpression loopVar;
            Expression initValue, condition, increment;
            VisitForIterator(ctx.GetChild(2) as DDVParser.ForIteratorContext, out loopVar, out initValue, out condition, out increment);
            var body = VisitBodyBlock(ctx.children.Skip(5).Take(ctx.ChildCount - 6));
            return For(loopVar, initValue, condition, increment, body);
        }

        protected void VisitForIterator(DDVParser.ForIteratorContext ctx, out ParameterExpression loopVar, out Expression initValue, out Expression condition, out Expression increment)
        {
            /* 
            forIterator
	            :	assignment SemiColon binaryExpression SemiColon assignment
	            ; 
            */
            var assign = Visit(ctx.GetChild(0)) as BinaryExpression;
            loopVar = assign.Left as ParameterExpression;
            initValue = assign.Right;
            condition = Visit(ctx.GetChild(2));
            increment = Visit(ctx.GetChild(4));
        }

        protected virtual Expression VisitFunctionExpression(DDVParser.FunctionExpressionContext ctx)
        {
            /* 
                functionExpression
	            :	function semiColon
	            |	forLoopExpression 
	            ; 
            */
            var func = Visit(ctx.GetChild(0));
            var pushFrame = Expression.Call(null,
                                            typeof(CodeContext).GetMethod("PushFrame", BindingFlags.Public | BindingFlags.Static),
                                            Expression.Constant(ctx.GetText()),
                                            Expression.Constant(ctx.Start.Line),
                                            Expression.Constant(ConsoleColor.Yellow));
            var popFrame = Expression.Call(null,
                                           typeof(CodeContext).GetMethod("PopFrame", BindingFlags.Public | BindingFlags.Static),
                                           Expression.Constant(null, typeof(string)),
                                           Expression.Constant(ConsoleColor.Yellow));
            var block = CreateBlock(new ParameterExpression[0], new Expression[] { pushFrame, func });
            return Expression.TryCatchFinally(block, popFrame);
        }

        protected virtual Expression VisitBinaryExpression(IParseTree ctx)
        {
            /* binaryExpression
                    :	(function | literal | propertyAccess | arithmeticExpression) booleanOperator (function | literal | propertyAccess | arithmeticExpression)
                    |	booleanLiteral
                    |	propertyAccess 
                    |   function
                    ;
                */
            if (ctx.ChildCount == 1)
            {
                //| booleanLiteral
                //| propertyAccess
                //| function
                return Visit(ctx.GetChild(0));
            }
            else if (ctx.ChildCount >= 3)
            {
                // (function | literal | propertyAccess | arithmeticExpression) booleanOperator (function | literal | propertyAccess | arithmeticExpression)
                var op = ctx.GetChild(1).GetText();
                var left = ctx.GetChild(0);
                var right = ctx.GetChild(2);
                Expression leftExp = null; //Visit(left);
                Expression rightExp = null; // Visit(right);
                if (left is DDVParser.NullLiteralContext)
                {
                    rightExp = Visit(right);
                    using (var scope = new ExpressionScope(rightExp.Type, "right", rightExp, ExpressionScope.Current?.IsAssignment ?? false))
                        leftExp = Visit(left);
                }
                else
                {
                    leftExp = Visit(left);
                    using (var scope = new ExpressionScope(leftExp.Type, "left", leftExp, ExpressionScope.Current?.IsAssignment ?? false))
                        rightExp = Visit(right);
                }

                if (!leftExp.Type.Equals(rightExp.Type))
                {
                    var leftSize = leftExp.Type.IsValueType ? Marshal.SizeOf(leftExp.Type) : 0;
                    var rightSize = rightExp.Type.IsValueType ? Marshal.SizeOf(rightExp.Type) : 0;

                    if (leftSize > rightSize)
                    {
                        rightExp = Convert(rightExp, leftExp.Type);
                    }
                    else if (rightSize > leftSize)
                    {
                        leftExp = Convert(leftExp, rightExp.Type);
                    }
                    else
                    {
                        if (leftExp.Type.Equals(typeof(float)))
                        {
                            rightExp = Convert(rightExp, typeof(float));
                        }
                        else if (leftExp.Type.Equals(typeof(double)))
                        {
                            rightExp = Convert(rightExp, typeof(double));
                        }
                        else if (rightExp.Type.Equals(typeof(float)))
                        {
                            leftExp = Convert(leftExp, typeof(float));
                        }
                        else if (rightExp.Type.Equals(typeof(double)))
                        {
                            leftExp = Convert(leftExp, typeof(double));
                        }
                        else
                        {
                            rightExp = Convert(rightExp, leftExp.Type);
                        }
                    }
                }

                switch (op)
                {
                    case "==":
                    case "=":
                        {
                            if (leftExp.Type.Implements<IModel>() && rightExp.Type.Implements<IModel>())
                            {
                                var mi = typeof(IModelEx).GetMethod("GlobalKey", BindingFlags.Public | BindingFlags.Static);
                                return Expression.Equal(Expression.Call(null, mi, leftExp), Expression.Call(null, mi, rightExp));
                            }
                            else
                            {
                                if (leftExp.Type.Equals(rightExp.Type) && !leftExp.Type.Equals(typeof(object)))
                                {
                                    return Expression.Equal(leftExp, rightExp);
                                }
                                else
                                {
                                    return Expression.Call(null, typeof(object).GetMethod("Equals", BindingFlags.Public | BindingFlags.Static), leftExp, rightExp);
                                }
                            }
                        }
                    case ">":
                        {
                            if ((leftExp is MemberExpression && ((MemberExpression)leftExp).Member.DeclaringType.Equals(typeof(PropertyInvoker)))
                                || (rightExp is MemberExpression && ((MemberExpression)rightExp).Member.DeclaringType.Equals(typeof(PropertyInvoker))))
                            {
                                var method = typeof(Invoker).GetMethod("GT", BindingFlags.Public | BindingFlags.Static);
                                return Expression.Call(null, method, ToObject(leftExp), ToObject(rightExp));
                            }
                            else
                            {
                                return Expression.GreaterThan(leftExp, rightExp);
                            }
                        }
                    case "<":
                        {
                            if ((leftExp is MemberExpression && ((MemberExpression)leftExp).Member.DeclaringType.Equals(typeof(PropertyInvoker)))
                                || (rightExp is MemberExpression && ((MemberExpression)rightExp).Member.DeclaringType.Equals(typeof(PropertyInvoker))))
                            {
                                var method = typeof(Invoker).GetMethod("LT", BindingFlags.Public | BindingFlags.Static);
                                return Expression.Call(null, method, ToObject(leftExp), ToObject(rightExp));
                            }
                            else
                            {
                                return Expression.LessThan(leftExp, rightExp);
                            }
                        }
                    case ">=":
                        {
                            if ((leftExp is MemberExpression && ((MemberExpression)leftExp).Member.DeclaringType.Equals(typeof(PropertyInvoker)))
                                || (rightExp is MemberExpression && ((MemberExpression)rightExp).Member.DeclaringType.Equals(typeof(PropertyInvoker))))
                            {
                                var method = typeof(Invoker).GetMethod("GTE", BindingFlags.Public | BindingFlags.Static);
                                return Expression.Call(null, method, ToObject(leftExp), ToObject(rightExp));
                            }
                            else
                            {
                                return Expression.GreaterThanOrEqual(leftExp, rightExp);
                            }
                        }
                    case "<=":
                        {
                            if ((leftExp is MemberExpression && ((MemberExpression)leftExp).Member.DeclaringType.Equals(typeof(PropertyInvoker)))
                                || (rightExp is MemberExpression && ((MemberExpression)rightExp).Member.DeclaringType.Equals(typeof(PropertyInvoker))))
                            {
                                var method = typeof(Invoker).GetMethod("LTE", BindingFlags.Public | BindingFlags.Static);
                                return Expression.Call(null, method, ToObject(leftExp), ToObject(rightExp));
                            }
                            else
                            {
                                return Expression.LessThanOrEqual(leftExp, rightExp);
                            }
                        }
                    case "!=":
                        {
                            if (leftExp.Type.Implements<IModel>() && rightExp.Type.Implements<IModel>())
                            {
                                var mi = typeof(IModelEx).GetMethod("GlobalKey", BindingFlags.Public | BindingFlags.Static);
                                return Expression.NotEqual(Expression.Call(null, mi, leftExp), Expression.Call(null, mi, rightExp));
                            }
                            else
                            {
                                if (leftExp.Type.Equals(rightExp.Type) && !leftExp.Type.Equals(typeof(object)))
                                {
                                    return Expression.NotEqual(leftExp, rightExp);
                                }
                                else
                                {
                                    return Expression.Not(Expression.Call(null, typeof(object).GetMethod("Equals", BindingFlags.Public | BindingFlags.Static), leftExp, rightExp));
                                }
                            }
                        }
                    default:
                        throw new NotSupportedException("The equality operator provided is not supported.");
                }
            }
            else
                throw new NotSupportedException("The binary expression format provided is not supported.");
        }

        protected virtual Expression ToObject(Expression exp)
        {
            if (exp.Type.Equals(typeof(object))) return exp;
            return Convert(exp, typeof(object));
        }

        protected virtual Expression VisitLogicalExpression(DDVParser.LogicalExpressionContext ctx)
        {
            /* logicalExpression
                    :	'(' logicalExpression ')'
                    |	(booleanLiteral | binaryExpression)
                    |	(booleanLiteral | binaryExpression) logicalOperator (booleanLiteral | binaryExpression)
                    |	logicalExpression logicalOperator logicalExpression
                    ;	
                */
            if (ctx.ChildCount == 1)
            {
                // (booleanLiteral | binaryExpression)
                return Visit(ctx.GetChild(0));
            }
            else if (ctx.GetChild(1) is DDVParser.LogicalOperatorContext)
            {
                var left = ctx.GetChild(0);
                var right = ctx.GetChild(2);
                var leftExp = Visit(left);
                var rightExp = Visit(right);
                return VisitLogicalOperator(leftExp, ctx.GetChild(1) as DDVParser.LogicalOperatorContext, rightExp);
            }
            else if (ctx.GetChild(0) is TerminalNodeImpl && ((TerminalNodeImpl)ctx.GetChild(0)).GetText() == "(")
            {
                // '(' logicalExpression ')'
                return Visit(ctx.GetChild(1));
            }
            throw new NotSupportedException("The logical expression provided is not in a valid format");
        }

        protected virtual Expression VisitLogicalOperator(Expression left, DDVParser.LogicalOperatorContext ctx, Expression right)
        {
            // (booleanLiteral | binaryExpression) logicalOperator (booleanLiteral | binaryExpression)
            // | logicalExpression logicalOperator logicalExpression
            var op = ctx.GetText();

            switch (op)
            {
                case "&&":
                case "and":
                    {
                        return Expression.AndAlso(left, right);
                    }
                case "||":
                case "or":
                    {
                        return Expression.OrElse(left, right);
                    }
                default:
                    throw new NotSupportedException("The logical operator provided is not supported.");
            }
        }

        protected virtual Expression VisitLiteral(DDVParser.LiteralContext ctx)
        {
            /* literal
                :   integerLiteral
                |   booleanLiteral
	            |	floatLiteral
	            |   stringLiteral
                |   nullLiteral
                ;
            */
            return Visit(ctx.GetChild(0));
        }

        protected virtual Expression VisitBooleanLiteral(DDVParser.BooleanLiteralContext ctx)
        {
            /* booleanLiteral
                    :   'true'
                    |   'false'
                    ;
                */
            var value = bool.Parse(ctx.GetText());
            if (ExpressionScope.Current?.InstanceType.IsNullable() ?? false)
            {
                var genArg = ExpressionScope.Current.InstanceType.GetGenericArguments()[0];
                using (var scope = new ExpressionScope(genArg, ExpressionScope.Current.InstanceName, ExpressionScope.Current.Source, ExpressionScope.Current.IsAssignment, ExpressionScope.Current.Args))
                {
                    return NewNullable(VisitBooleanLiteral(ctx), genArg);
                }
            }
            else
            {
                return Expression.Constant(value, typeof(bool));
            }
        }

        protected virtual Expression VisitNull(DDVParser.NullLiteralContext ctx)
        {
            if (ExpressionScope.Current?.InstanceType.IsValueType ?? false)
            {
                throw new InvalidOperationException("A value type can never equal null.");
            }
            return Expression.Constant(null, ExpressionScope.Current?.InstanceType == null ? typeof(object) : ExpressionScope.Current.InstanceType);
        }

        protected virtual Expression VisitFloat(DDVParser.FloatLiteralContext ctx)
        {
            if (ExpressionScope.Current == null || ExpressionScope.Current.InstanceType.Equals(typeof(double)))
            {
                return Expression.Constant(double.Parse(ctx.GetText()), typeof(double));
            }
            else if (ExpressionScope.Current.InstanceType.Equals(typeof(float)))
            {
                return Expression.Constant(float.Parse(ctx.GetText()), typeof(float));
            }
            else if (ExpressionScope.Current.InstanceType.Equals(typeof(decimal)))
            {
                return Expression.Constant(decimal.Parse(ctx.GetText()), typeof(decimal));
            }
            else if ((ExpressionScope.Current.InstanceType.IsGenericType && ExpressionScope.Current.InstanceType.GetGenericTypeDefinition() == typeof(Func<>)) )
            {
                var genArg = ExpressionScope.Current.InstanceType.GetGenericArguments()[0];
                using (var scope = new ExpressionScope(genArg, ExpressionScope.Current.InstanceName, ExpressionScope.Current.Source, ExpressionScope.Current.IsAssignment))
                {
                    return VisitFloat(ctx);
                }
            }
            else if (ExpressionScope.Current?.InstanceType.IsNullable() ?? false)
            {
                var genArg = ExpressionScope.Current.InstanceType.GetGenericArguments()[0];
                using (var scope = new ExpressionScope(genArg, ExpressionScope.Current.InstanceName, ExpressionScope.Current.Source, ExpressionScope.Current.IsAssignment, ExpressionScope.Current.Args))
                {
                    return NewNullable(VisitFloat(ctx), genArg);
                }
            }
            throw new NotSupportedException(string.Format("The current binary equality type of '{0}' is not a valid floating point type.", ExpressionScope.Current.InstanceType.Name));
        }

        protected virtual Expression VisitStringLiteral(DDVParser.StringLiteralContext ctx)
        {
            /* stringLiteral
                :	StringLiteral
                ;
            */
            DateTime dt;
            var text = ctx.GetString();
            if (ExpressionScope.Current?.InstanceType.IsNullable() ?? false)
            {
                var genArg = ExpressionScope.Current.InstanceType.GetGenericArguments()[0];
                using (var scope = new ExpressionScope(genArg, ExpressionScope.Current.InstanceName, ExpressionScope.Current.Source, ExpressionScope.Current.IsAssignment, ExpressionScope.Current.Args))
                {
                    return NewNullable(VisitStringLiteral(ctx), genArg);
                }
            }
            else if (((ExpressionScope.Current?.InstanceType == null) || ExpressionScope.Current?.InstanceType == typeof(DateTime)) && DateTime.TryParse(text, out dt))
            {
                return Expression.Constant(dt);
            }
            else if (ExpressionScope.Current?.InstanceType.IsEnum ?? false)
            {
                var enumNames = text.Split(new char[] { '|' }, StringSplitOptions.RemoveEmptyEntries);
                long value = 0;
                foreach(var enumName in enumNames)
                {
                    long temp;
                    Enum.Parse(ExpressionScope.Current.InstanceType, enumName.Trim()).TryCast<long>(out temp);
                    value |= temp;
                }
                object enumValue;
                if (value.TryCast(ExpressionScope.Current.InstanceType, out enumValue))
                    return Expression.Constant(enumValue, ExpressionScope.Current.InstanceType);
                else
                    throw new InvalidOperationException(string.Format("The Enum value of {0} could not be cast to Enum type '{1}'.", value.ToString(), ExpressionScope.Current.InstanceType.Name));
            }
            else
            {
                text = text.Replace("\\\\", "\\");
                return Expression.Constant(text);
            }
        }

        protected virtual Expression VisitNumber(DDVParser.NumberLiteralContext ctx)
        {
            return Visit(ctx.GetChild(0));
        }

        protected virtual Expression VisitInteger(DDVParser.IntegerLiteralContext ctx)
        {
            if (ExpressionScope.Current == null || ExpressionScope.Current.InstanceType.Equals(typeof(long)) || ExpressionScope.Current.InstanceType.Equals(typeof(object)))
            {
                return Expression.Constant(long.Parse(ctx.GetText()), typeof(long));
            }
            else if (ExpressionScope.Current.InstanceType.Equals(typeof(sbyte)))
            {
                return Expression.Constant(sbyte.Parse(ctx.GetText()), typeof(sbyte));
            }
            else if (ExpressionScope.Current.InstanceType.Equals(typeof(short)))
            {
                return Expression.Constant(short.Parse(ctx.GetText()), typeof(short));
            }
            else if (ExpressionScope.Current.InstanceType.Equals(typeof(ushort)))
            {
                return Expression.Constant(ushort.Parse(ctx.GetText()), typeof(ushort));
            }
            else if (ExpressionScope.Current.InstanceType.Equals(typeof(int)))
            {
                return Expression.Constant(int.Parse(ctx.GetText()), typeof(int));
            }
            else if (ExpressionScope.Current.InstanceType.Equals(typeof(uint)))
            {
                return Expression.Constant(uint.Parse(ctx.GetText()), typeof(uint));
            }
            else if (ExpressionScope.Current.InstanceType.Equals(typeof(byte)))
            {
                return Expression.Constant(byte.Parse(ctx.GetText()), typeof(byte));
            }
            else if (ExpressionScope.Current.InstanceType.Equals(typeof(ulong)))
            {
                return Expression.Constant(ulong.Parse(ctx.GetText()), typeof(ulong));
            }
            else if (ExpressionScope.Current.InstanceType.Equals(typeof(double)))
            {
                return Convert(Expression.Constant(long.Parse(ctx.GetText()), typeof(long)), typeof(double));
            }
            else if (ExpressionScope.Current.InstanceType.Equals(typeof(float)))
            {
                return Convert(Expression.Constant(int.Parse(ctx.GetText()), typeof(int)), typeof(float));
            }
            else if (ExpressionScope.Current.InstanceType.Equals(typeof(decimal)))
            {
                return Convert(Expression.Constant(ulong.Parse(ctx.GetText()), typeof(ulong)), typeof(decimal));
            }
            else if ((ExpressionScope.Current.InstanceType.IsGenericType && ExpressionScope.Current.InstanceType.GetGenericTypeDefinition() == typeof(Func<>)))
            {
                var genArg = ExpressionScope.Current.InstanceType.GetGenericArguments()[0];
                using (var scope = new ExpressionScope(genArg, ExpressionScope.Current.InstanceName, ExpressionScope.Current.Source, ExpressionScope.Current.IsAssignment, ExpressionScope.Current.Args))
                {
                    return VisitInteger(ctx);
                }
            }
            else if (ExpressionScope.Current?.InstanceType.IsNullable() ?? false)
            {
                var genArg = ExpressionScope.Current.InstanceType.GetGenericArguments()[0];
                using (var scope = new ExpressionScope(genArg, ExpressionScope.Current.InstanceName, ExpressionScope.Current.Source, ExpressionScope.Current.IsAssignment, ExpressionScope.Current.Args))
                {
                    return NewNullable(VisitInteger(ctx), genArg);
                }
            }
            throw new NotSupportedException(string.Format("The current binary equality type of '{0}' is not a valid integer type.", ExpressionScope.Current.InstanceType.Name));
        }

        protected virtual Expression NewNullable(Expression value, Type valueType)
        {
            var ctor = typeof(Nullable<>).MakeGenericType(valueType).GetConstructor(new Type[] { valueType });
            return Expression.New(ctor, value);
        }

        protected virtual Expression VisitArithemticOperator(DDVParser.ArithmeticOperatorContext ctx)
        {
            return Expression.Constant(ctx.GetText(), typeof(string));
        }

        protected class ArithmeticOperation
        {
            public ArithmeticOperation(Expression value, string op, ArithmeticOperation right = null)
            {
                this.Value = value;
                this.Right = right;
                this.Operator = op;
            }

            public Expression Value { get; set; }
            public string Operator { get; set; }
            public ArithmeticOperation Right { get; set; }

        }

        protected virtual Expression VisitArithmetic(DDVParser.ArithmeticExpressionContext ctx)
        {
            /* 
            arithmeticExpression
	            :	OpenParen arithmeticExpression CloseParen
	            |	(numberLiteral | propertyAccess | function | variable | lambdaVariable | scopedVariable )
	            |	(numberLiteral | propertyAccess | function | variable | lambdaVariable | scopedVariable ) arithmeticOperator (numberLiteral | propertyAccess | function | variable | lambdaVariable | scopedVariable)
	            |	arithmeticExpression (arithmeticOperator arithmeticExpression)+
	            ;
            */
            if (ctx.ChildCount == 1)
            {
                return Visit(ctx.GetChild(0)); // it's just a value
            }
            else if (ctx.GetChild(1) is DDVParser.ArithmeticExpressionContext)
            {
                return Visit(ctx.GetChild(1));
            }
            else
            {
                var ops = new List<ArithmeticOperation>();
                for (int i = 1; i <= ctx.ChildCount; i += 2)
                {
                    var value = Visit(ctx.GetChild(i - 1));
                    var op = Visit(ctx.GetChild(i));
                    var ao = new ArithmeticOperation(value, op == null ? "+" : ((ConstantExpression)op).Value.ToString());
                    if (ops.Count > 0)
                    {
                        ops.Last().Right = ao;
                    }
                    ops.Add(ao);
                }

                if (ops.Count % 2 > 0)
                {
                    // we want an even number of values in the list, so that our binary math expressions will always have a left and right side
                    // so we just add a + 0 to the end of the list
                    var ao = new ArithmeticOperation(Expression.Constant(Activator.CreateInstance(ops[0].Value.Type), ops[0].Value.Type), "+");
                    ops.Last().Right = ao;
                    ops.Add(ao);
                }

                // in the first pass, convert all the * or / to expressions, then process all the adds
                var index = 0;
                Func<string, bool> selector = (op) => op.Equals("*") || op.Equals("/");
                do
                {
                    while (index < ops.Count - 1) // we dont compute for right-most item, since it has no linked partner
                    {
                        // the goal is to combine the current item with the item to its right into a new expression
                        // if the item passes the operator selector
                        var item = ops[index];
                        if (selector(item.Operator))
                        {
                            // selector passed, compute the new expression for the current item and the item to its right
                            var value = VisitArithmeticOperation(item);
                            item.Value = value;
                            item.Operator = item.Right == null ? "+" : item.Right.Operator;
                            if (item.Right != null)
                            {
                                item.Right = item.Right.Right;
                            }
                            if (ops.Count > index + 1)
                            {
                                ops.RemoveAt(index + 1);
                            }
                        }
                        index++;
                    }

                    index = 0;
                    selector = (op) => op.Equals("+") || op.Equals("-");
                } while (ops.Count > 1);

                return ops[0].Value; // the remaining item will be the completed expression tree
            }
        }

        protected virtual Expression VisitArithmeticOperation(ArithmeticOperation op)
        {
            Expression left = op.Value, right = op.Right.Value;

            Type leftType = left.Type, rightType = right.Type;
            bool leftTypeIsSupported = false, rightTypeIsSupported = false;
            if (!leftType.IsValueType && leftType != typeof(object) && leftType.IsGenericType && leftType.GetGenericTypeDefinition() == typeof(Func<>))
            {
                var genArg = leftType.GetGenericArguments()[0];
                if (genArg.IsValueType || genArg == typeof(object))
                {
                    leftType = genArg;
                    leftTypeIsSupported = true;
                }
            }
            else
            {
                leftTypeIsSupported = true;
            }

            if (!rightType.IsValueType && rightType != typeof(object) && rightType.IsGenericType && rightType.GetGenericTypeDefinition() == typeof(Func<>))
            {
                var genArg = rightType.GetGenericArguments()[0];
                if (genArg.IsValueType || genArg == typeof(object))
                {
                    rightType = genArg;
                    rightTypeIsSupported = true;
                }
            }
            else
            {
                rightTypeIsSupported = true;
            }

            if (!leftTypeIsSupported || !rightTypeIsSupported)
            {
                throw new InvalidOperationException(string.Format("Arithmetic operations are not defined between {0} and {1} types.", leftType.Name, rightType.Name));
            }

            if (!leftType.Equals(rightType))
            {
                var leftSize = leftType.IsValueType ? Marshal.SizeOf(leftType) : int.MaxValue;
                var rightSize = rightType.IsValueType ? Marshal.SizeOf(rightType) : int.MaxValue;

                if (leftSize > rightSize)
                {
                    right = Convert(right, leftType);
                }
                else if (rightSize > leftSize)
                {
                    left = Convert(left, rightType);
                }
                else
                {
                    if (leftType.Equals(typeof(float)))
                    {
                        right = Convert(right, typeof(float));
                    }
                    else if (leftType.Equals(typeof(double)))
                    {
                        right = Convert(right, typeof(double));
                    }
                    else if (rightType.Equals(typeof(float)))
                    {
                        left = Convert(left, typeof(float));
                    }
                    else if (rightType.Equals(typeof(double)))
                    {
                        left = Convert(left, typeof(double));
                    }
                    else
                    {
                        right = Convert(right, leftType);
                    }
                }
            }

            switch (op.Operator)
            {
                case "*":
                    {
                        if (!leftType.Equals(typeof(double)) && !leftType.Equals(typeof(decimal)))
                        {
                            left = Expression.Convert(left, typeof(double));
                        }
                        if (!rightType.Equals(typeof(double)) && !rightType.Equals(typeof(decimal)))
                        {
                            right = Expression.Convert(right, typeof(double));
                        }
                        return Expression.Multiply(left, right);
                    }
                case "/":
                    {
                        if (!leftType.Equals(typeof(double)) && !leftType.Equals(typeof(decimal)))
                        {
                            left = Expression.Convert(left, typeof(double));
                        }
                        if (!rightType.Equals(typeof(double)) && !rightType.Equals(typeof(decimal)))
                        {
                            right = Expression.Convert(right, typeof(double));
                        }
                        return Expression.Divide(left, right);
                    }
                case "+":
                    {
                        if (!leftType.Equals(typeof(double)) && !leftType.Equals(typeof(decimal)))
                        {
                            left = Expression.Convert(left, typeof(double));
                        }
                        if (!rightType.Equals(typeof(double)) && !rightType.Equals(typeof(decimal)))
                        {
                            right = Expression.Convert(right, typeof(double));
                        }
                        return Expression.Add(left, right);
                    }
                case "-":
                    {
                        if (!leftType.Equals(typeof(double)) && !leftType.Equals(typeof(decimal)))
                        {
                            left = Expression.Convert(left, typeof(double));
                        }
                        if (!rightType.Equals(typeof(double)) && !rightType.Equals(typeof(decimal)))
                        {
                            right = Expression.Convert(right, typeof(double));
                        }
                        return Expression.Subtract(left, right);
                    }
                case "%":
                    return Expression.Modulo(left, right);
                case "^":
                    return Expression.ExclusiveOr(left, right);
                case "|":
                    return Expression.Or(left, right);
                case "&":
                    return Expression.And(left, right);
                default:
                    throw new NotSupportedException(string.Format("An arithemtic operator of type '{0}' is not supported.", op.Operator));
            }
        }

        protected int SortArithmeticOperators(ArithmeticOperation op1, ArithmeticOperation op2)
        {
            if (op1.Operator == "*" && op2.Operator == "*")
            {
                return 0;
            }
            else if (op1.Operator == "*")
            {
                return 1;
            }
            else if (op2.Operator == "*")
            {
                return -1;
            }
            else if (op1.Operator == "/" && op2.Operator == "/")
            {
                return 0;
            }
            else if (op1.Operator == "/")
            {
                return 1;
            }
            else if (op2.Operator == "/")
            {
                return -1;
            }
            else if (op1.Operator == "+" && op2.Operator == "+")
            {
                return 0;
            }
            else if (op1.Operator == "+")
            {
                return 1;
            }
            else if (op2.Operator == "+")
            {
                return -1;
            }
            else if (op1.Operator == "-" && op2.Operator == "-")
            {
                return 0;
            }
            else if (op1.Operator == "-")
            {
                return 1;
            }
            else if (op2.Operator == "-")
            {
                return -1;
            }
            else
            {
                return 0;
            }
        }

        protected virtual Expression VisitArrayOfLiterals(DDVParser.ArrayOfLiteralsContext ctx)
        {
            /* 
             
            arrayOfLiterals
	            :	BracketOpen ((literal | propertyAccess | methodAccess | variable | arrayOfLiterals | arithmeticExpression | binaryExpression | function) 
					            (Comma (literal | propertyAccess | methodAccess | variable | arrayOfLiterals | arithmeticExpression | binaryExpression | function))*)?
		            BracketClose
	            ;

            */

            var count = ctx.ChildCount - 1;
            var literals = new List<Expression>();
            for (int i = 1; i < count; i += 2)
            {
                literals.Add(Visit(ctx.GetChild(i)));
            }
            Type elementType = literals.Count > 0 ? GetSourceType(literals.FirstOrDefault()) : typeof(object);
            foreach (var item in literals.Skip(1))
            {
                if (GetSourceType(item) != elementType)
                {
                    elementType = typeof(object);
                }
            }
            if (literals.Count > 1)
            {
                foreach (var literal in literals.Skip(1))
                {
                    if (!GetSourceType(literal).Equals(elementType))
                    {
                        elementType = typeof(object);
                        for (int i = 0; i < literals.Count; i++)
                        {
                            literals[i] = Convert(literals[i], elementType);
                        }
                        break;
                    }
                }
            }
            var listType = (ExpressionScope.Current?.InstanceType.Implements<IList>() ?? false) ? ExpressionScope.Current?.InstanceType : typeof(List<>).MakeGenericType(elementType);
            var listCtor = listType.GetConstructor(new Type[] { typeof(IEnumerable<>).MakeGenericType(elementType) });
            return Expression.New(listCtor, Expression.NewArrayInit(elementType, literals.ToArray()));
        }

        protected virtual Expression VisitVariableDeclaration(DDVParser.VariableDeclarationContext ctx)
        {
            //variableDeclaration
            //  :	'var' variable
            //  ;

            var variableName = ctx.GetChild(1).GetText();
            var variable = VariableScope.Search(variableName);
            if (variable != null)
            {
                throw new InvalidOperationException(string.Format("A variable named '{0}' has already been declared in the current scope.", variableName));
            }
            var scope = ExpressionScope.Current;
            if (scope == null)
                throw new InvalidOperationException(string.Format("The variable type for '{0}' could not be determined.", variableName));
            variable = Expression.Variable(scope.InstanceType, variableName);
            //if (IsCodeBlock)
            //{
            //    VariableScope.Root.Add(variableName, variable);
            //}
            //else
            //{
            VariableScope.Current.Add(variableName, variable);
            //}
            return variable;
        }

        protected virtual Expression VisitVariable(DDVParser.VariableContext ctx)
        {
            //variable
            //    : At namedElement
            //    ;

            var variableName = ctx.GetText();
            Expression variable = null;
            if (!TryGetParameter(variableName, out variable))
            {
                if (!TryGetRuntimeProperty(variableName, out variable))
                {
                    throw new InvalidOperationException("The variable specified '" + variableName + "' could not be found.");
                }
            }
            return variable;
        }


        protected virtual Expression VisitMethodAccess(DDVParser.MethodAccessContext ctx)
        {
            /*
                method
	                :	(dot function)+
	                ;

                methodAccess
	                :	variable method?
	                ;

            */
            var variable = Visit(ctx.GetChild(0));
            if (ctx.ChildCount == 1)
            {
                // just return the model property itself from the runtime
                return variable;
            }
            else if (ctx.ChildCount == 2)
            {
                using (var scope = new ExpressionScope(variable.Type, ctx.GetChild(0).GetText(), variable, ExpressionScope.Current?.IsAssignment ?? false, ExpressionScope.Current?.Args))
                    return VisitMethod(variable, ctx.GetChild(1) as DDVParser.MethodContext);
            }
            throw new NotSupportedException(string.Format("A parse tree child count of '{0}' is not for a property accessor.", ctx.ChildCount));
        }

        protected virtual Expression VisitMethod(Expression source, DDVParser.MethodContext ctx)
        {
            /*
               property
	                :	(dot namedElement ('[' (literal | propertyAccess | methodAccess | arithmeticExpression | function) ']')? )+
	                ;
               method
	                :	((dot function) | property)* (dot function)
	                ;
                function
	                :	namedElement OpenParen CloseParen
	                |	namedElement OpenParen (literal | propertyAccess | methodAccess | variable | arrayOfLiterals | arithmeticExpression | binaryExpression | function) 
                                               (Comma (literal | propertyAccess | methodAccess | variable | arrayOfLiterals | arithmeticExpression | binaryExpression | function))* 
                                     CloseParen	
	                ;
            */


            var currentSource = source;

            for (int i = 0; i < ctx.ChildCount; i++)
            {
                if (ctx.GetChild(i) is DDVParser.PropertyContext)
                {
                    // we're on a property, so evaluate it and move on
                    using (var scope = new ExpressionScope(currentSource.Type, "property", currentSource, false))
                        currentSource = VisitProperty(currentSource, ctx.GetChild(i) as DDVParser.PropertyContext);
                }
                else
                {
                    i++; // its a function, and we're on a dot (.) node
                    var sourceType = GetSourceType(currentSource);
                    var memberName = ctx.GetChild(i).GetText();
                    if (sourceType.Implements<IModel>() && memberName.Equals("Owner"))
                    {
                        var methodInfo = typeof(IRuntime).GetMethod("GetOwner");
                        currentSource = Expression.Call(this.Runtime, methodInfo, currentSource);
                    }
                    else
                    {
                        using (var scope = new ExpressionScope(currentSource.Type, memberName, currentSource, false, ExpressionScope.Current?.Args))
                            currentSource = VisitFunction(ctx.GetChild(i) as DDVParser.FunctionContext);
                    }
                }
            }
            return currentSource;
        }

        protected virtual Expression VisitFunction(DDVParser.FunctionContext ctx)
        {
            /* 
                function
	                :	(variable | lambdaVariable | namedElement) OpenParen CloseParen
	                |	(variable | lambdaVariable | namedElement) OpenParen parameter (Comma parameter)* CloseParen	
	                ;

                parameter
	                :	literal | propertyAccess | methodAccess | variable | lambdaVariable | arrayOfLiterals 
	                |	arithmeticExpression | binaryExpression | function | ternaryFunction | objectExpression | functionDefinition
	                ;
            */
            if (ctx.GetChild(0) is DDVParser.VariableContext)
            {
                return VisitLambdaFunction(ctx);
            }

            var functionName = ctx.GetChild(0).GetText();
            MethodInfo method = null;
            var argExps = new List<Expression>();
            Expression target = ExpressionScope.Current?.Source ?? this.Runtime;
            Type targetType = ExpressionScope.Current?.InstanceType ?? this.Runtime.Type;

            Expression special;
            if (TryVisitSpecialFunction(functionName, ctx, out special))
            {
                return special;
            }

            if (ExpressionScope.Current != null && ExpressionScope.Current.InstanceType.Implements(typeof(IEnumerable<>)))
            {
                Expression exp;
                if (TryVisitLinqMethod(ctx, out exp))
                {
                    return exp;
                }
            }

            if (ctx.ChildCount == 3)
            {
                method = targetType.GetMethod(functionName, BindingFlags.Public | BindingFlags.Instance, null, Type.EmptyTypes, null);
            }
            if (method == null)
            {
                var args = new List<IParseTree>();
                var argumentCount = ctx.ChildCount - 3;
                argumentCount = argumentCount - (argumentCount / 2);
                var methods = targetType.GetMethods(BindingFlags.Public | BindingFlags.Instance)
                    .Where(mi => mi.Name.Equals(functionName))
                    .ToArray();

                if (methods.Length == 0)
                {
                    // check the runtime explicitly
                    methods = this.RuntimeType.GetMethods(BindingFlags.Public | BindingFlags.Instance)
                                        .Where(mi => mi.Name.Equals(functionName))
                                        .ToArray();
                }

                var invokeLate = false;

                if (methods.Length == 0)
                {
                    // look for a delegate field type that might match
                    var fields = targetType.GetFields(BindingFlags.Public | BindingFlags.Instance)
                        .Where(fi => fi.Name.Equals("_" + functionName) && fi.FieldType.IsSubclassOf(typeof(Delegate)) && fi.FieldType.IsGenericType)
                        .ToArray();

                    Expression funcExp;
                    if (fields.Count() == 0
                        && ExpressionScope.Current?.Source is ParameterExpression
                        && ((ParameterExpression)ExpressionScope.Current.Source).Name == "@@this"
                        && (ExpressionScope.Current.Args as Dictionary<string, Expression>) != null
                        && ((Dictionary<string, Expression>)ExpressionScope.Current.Args).TryGetValue(functionName, out funcExp))
                    {
                        return Expression.Invoke(Expression.Quote(funcExp), ((LambdaExpression)funcExp).Parameters);
                    }

                    FieldInfo field = null;
                    foreach (var fi in fields)
                    {
                        if (fi.FieldType.Name.StartsWith("Func") && fi.FieldType.GetGenericArguments().Length == argumentCount + 1)
                        {
                            field = fi;
                        }
                        else if (fi.FieldType.Name.StartsWith("Action") && fi.FieldType.GetGenericArguments().Length == argumentCount)
                        {
                            field = fi;
                        }
                        if (field != null) break;
                    }

                    if (field == null)
                    {
                        target = this.Runtime;
                        // see if it's an edge method
                        var modelTypeName = ModelTypeManager.ModelFullNames.FirstOrDefault(mn => mn.Replace(".", "_").Equals(functionName, StringComparison.CurrentCultureIgnoreCase));
                        if (string.IsNullOrEmpty(modelTypeName))
                        {
                            throw new NotSupportedException(string.Format("The specified function '{0}' does not exist.", functionName));
                        }
                        var modelType = ModelTypeManager.GetModelType(modelTypeName);
  
                        if (modelType.Implements<INamedModel>())
                        {
                            methods = new MethodInfo[] { this.RuntimeType.GetMethod("GetModel").MakeGenericMethod(modelType) };
                        }
                        else if (modelType.Implements<ILink>())
                        {
                            methods = new MethodInfo[] { this.RuntimeType.GetMethod("EdgeExists").MakeGenericMethod(modelType) };
                        }
                    }
                    else
                    {
                        // it's a lambda function on an inline type
                        for (int i = 2; i < ctx.ChildCount - 1; i += 2)
                        {
                            args.Add(ctx.GetChild(i));
                        }

                        var genArgs = field.FieldType.GetGenericArguments().ToList();

                        for (int i = 0; i < args.Count; i++)
                        {
                            var parm = genArgs[i];
                            var arg = args[i];
                            using (var scope = new ExpressionScope(parm, parm.Name, null, ExpressionScope.Current?.IsAssignment ?? false))
                                argExps.Add(Visit(arg));
                            if (!argExps[i].Type.Equals(parm))
                            {
                                argExps[i] = Convert(argExps[i], parm);
                            }
                        }

                        return Expression.Invoke(Expression.Field(target, field), argExps);
                    }
                }

                for (int i = 2; i < ctx.ChildCount; i += 2)
                {
                    args.Add(ctx.GetChild(i));
                }

                invokeLate = true;
                var possibleMethods = new List<MethodInfo>();
                foreach (var m in methods)
                {
                    var parms = m.GetParameters();

                    if (!(parms.Where(p => !p.IsOptional).Count() <= args.Count && parms.Length >= args.Count))
                    {
                        continue;
                    }
                    invokeLate = false;
                    possibleMethods.Add(m);
                }

                methods = possibleMethods.ToArray();

                if (invokeLate)
                {
                    var invokeMethod = typeof(Invoker).GetMethod("InvokeMethod", BindingFlags.Public | BindingFlags.Static);
                    methods = new MethodInfo[] { invokeMethod };
                }

                foreach (var m in methods)
                {
                    try
                    {
                        var parms = m.GetParameters();
                        if (invokeLate)
                        {
                            // public static object InvokeMethod(object source, string name, params object[] args)
                            argExps.Add(target);
                            argExps.Add(Expression.Constant(functionName));
                            var lateArgExps = new List<Expression>();
                            for (int i = 0; i < args.Count; i++)
                            {
                                var arg = args[i];
                                lateArgExps.Add(Visit(arg));
                            }
                            argExps.Add(Expression.NewArrayInit(typeof(object), lateArgExps));
                            target = null; // static call
                        }
                        else
                        {
                            for (int i = 0; i < parms.Length; i++)
                            {
                                var parm = parms[i];
                                if (i >= args.Count && parm.IsOptional)
                                {
                                    argExps.Add(Expression.Constant(parm.DefaultValue, parm.ParameterType));
                                }
                                else
                                {
                                    var arg = args[i];
                                    using (var scope = new ExpressionScope(parm.ParameterType, parm.Name, null, ExpressionScope.Current?.IsAssignment ?? false))
                                        argExps.Add(Visit(arg));
                                    if (!argExps[i].Type.Equals(parm))
                                    {
                                        argExps[i] = Convert(argExps[i], parm.ParameterType);
                                    }
                                }
                            }
                        }
                        // we made it this far without breaking, so the call will probably work
                        method = m;
                        break;
                    }
                    catch { }
                }
            }

            return Expression.Call(target, method, argExps.ToArray());
        }

        protected virtual bool TryVisitSpecialFunction(string functionName, DDVParser.FunctionContext ctx, out Expression special)
        {
            if (functionName == "Query")
            {
                special = VisitQueryFunction(ctx);
            }
            else if (functionName == "LT")
            {
                special = VisitLessThanFunction(ctx);
            }
            else if (functionName == "LTE")
            {
                special = VisitLessThanOrEqualsFunction(ctx);
            }
            else if (functionName == "GT")
            {
                special = VisitGreaterThanFunction(ctx);
            }
            else if (functionName == "GTE")
            {
                special = VisitGreaterThanOrEqualsFunction(ctx);
            }
            else if (functionName == "Concat" && !(ExpressionScope.Current?.Source.Type.Implements<IEnumerable>() ?? false))
            {
                special = VisitConcat(ctx);
            }
            else if (functionName == "Emit")
            {
                special = VisitEmit(ctx);
            }
            else if (functionName == "Clone")
            {
                special = VisitClone(ctx);
            }
            else if (functionName == "ToJson")
            {
                special = VisitToJson(ctx);
            }
            else
            {
                special = null;
                return false;
            }
            return special != null;
        }

        private Expression VisitToJson(DDVParser.FunctionContext ctx)
        {
            // Common.Serialization.JsonEx
            var toJson = typeof(JsonEx).GetMethod("ToJson", BindingFlags.Public | BindingFlags.Static).MakeGenericMethod(ExpressionScope.Current.Source.Type);
            return Expression.Call(null, toJson, ExpressionScope.Current.Source);
        }

        private Expression VisitClone(DDVParser.FunctionContext ctx)
        {
            if (ctx.ChildCount == 3)
            {
                if (ExpressionScope.Current.InstanceType.Implements<IModel>())
                {
                    return VisitCloneModel(ctx);
                }
                else
                {
                    return VisitCloneAnonymous(ctx);
                }
            }
            else
            {
                return null;
            }
        }

        private Expression VisitCloneAnonymous(DDVParser.FunctionContext ctx)
        {
            var type = ExpressionScope.Current.Source.Type;
            DDVParser.ObjectExpressionContext objCtx;
            lock(_objectDefs)
            {
                objCtx = _objectDefs[type];
            }
            var newExp = VisitObjectExpression(objCtx, type); // we already know the type, so pass it in to speed things up
            var statements = new List<Expression>();
            var fields = type.GetFields(BindingFlags.Instance | BindingFlags.Public);

            using (var scope = new VariableScope())
            {
                var obj = Expression.Variable(type, "@obj");

                VariableScope.Current.Add(obj.Name, obj);
                var returnLabel = Expression.Label(type, "return");
                statements.Add(Expression.Assign(obj, newExp));

                foreach (var field in fields.Where(f => !f.FieldType.IsGenericType || f.FieldType.GetGenericTypeDefinition() != typeof(Func<>)))
                {
                    statements.Add(Expression.Assign(Expression.Field(obj, field), Expression.Field(ExpressionScope.Current.Source, field)));
                }

                statements.Add(Expression.Return(returnLabel, obj));
                statements.Add(Expression.Label(returnLabel, Expression.Constant(null, type)));
                var newObj = CreateBlock(new ParameterExpression[] { obj }, statements.ToArray());
                return newObj;
            }
        }

        private Expression VisitCloneModel(DDVParser.FunctionContext ctx)
        {
            return Expression.Call(null, typeof(CodeContext).GetMethod("CloneModel", BindingFlags.NonPublic | BindingFlags.Static), ExpressionScope.Current.Source);
        }

        private static IModel CloneModel(IModel source)
        {
            var bytes = ((IBinarySerializable)source).ToBytes();
            var dest = (IModel)RuntimeModelBuilder.CreateModelInstance(source.ModelType);
            ((IBinarySerializable)dest).FromBytes(bytes);
            return dest;
        }

        protected virtual Expression VisitLambdaFunction(DDVParser.FunctionContext ctx)
        {
            var variableName = ctx.GetChild(0).GetText();
            var variable = VariableScope.Search(variableName);
            var funcType = variable.Type;
            var argExps = new List<Expression>();
            for (int i = 2; i < ctx.ChildCount - 1; i += 2)
            {
                argExps.Add(Visit(ctx.GetChild(i)));
            }
            return Expression.Invoke(variable, argExps);
        }

        protected virtual Expression VisitFunctionDefinitionContext(DDVParser.FunctionDefinitionContext ctx)
        {
            /* 
            returnExpression
	            :	Return parameter? SemiColon
	            ;

            typedArgument
	            :	namedElement WS unTypedParameter
	            ;

            unTypedArgument
	            :	Dollar namedElement
	            ;

            argument
	            :	typedArgument | unTypedArgument
	            ;

            functionDefinition
	            :	OpenParen argument (Comma argument)* CloseParen 
			            LambdaAssign 
			            OpenBrace 
				            (assignmentExpression | functionExpression | memberExpression)* returnExpression 
			            CloseBrace
	            ;
            */
            using (var scope = new VariableScope())
            {
                var arguments = new List<ParameterExpression>();
                var index = 1;
                while (ctx.GetChild(index).GetText() != ")")
                {
                    if (ctx.GetChild(index) is DDVParser.ArgumentContext)
                    {
                        var arg = VisitArgument(ctx.GetChild(index) as DDVParser.ArgumentContext);
                        arguments.Add(arg);
                        scope.Add(arg.Name, arg);
                    }
                    index++;
                }

                index += 3;

                var statements = new List<Expression>();
                var isCodeBlock = IsCodeBlock;
                IsCodeBlock = false;
                while (index < ctx.ChildCount - 1)
                {
                    var child = ctx.GetChild(index);
                    statements.Add(Visit(child));
                    if (child is DDVParser.ReturnExpressionContext)
                    {
                        break;
                    }
                    index++;
                }
                IsCodeBlock = isCodeBlock;
                if (statements.Last() == null)
                {
                    // we have an Action type lambda that returns no value
                    statements.RemoveAt(statements.Count - 1); // pull the null value out
                    var block = CreateBlock(
                        VariableFinder.FindParameters(statements),
                        statements.ToArray());
                    var lambda = Expression.Lambda(block, arguments);
                    return lambda;
                }
                else
                {
                    // we have a func type lambda that returns a value
                    var returnValue = statements.Last();
                    statements.RemoveAt(statements.Count - 1); // pull the null value out
                    var returnLabel = Expression.Label(returnValue.Type, "return");
                    statements.Add(Expression.Return(returnLabel, returnValue));
                    statements.Add(Expression.Label(returnLabel, Expression.Constant(returnValue.Type.IsValueType ? Activator.CreateInstance(returnValue.Type) : null, returnValue.Type)));
                    var block = CreateBlock(
                        VariableFinder.FindParameters(statements),
                        statements.ToArray());
                    var lambda = Expression.Lambda(block, arguments);
                    return lambda;
                }
            }
        }

        protected ParameterExpression VisitArgument(DDVParser.ArgumentContext ctx)
        {
            /* 
            typedArgument
	            :	namedElement WS unTypedParameter
	            ;

            unTypedArgument
	            :	Dollar namedElement
	            ;

            argument
	            :	typedArgument | unTypedArgument
	            ;
            */

            var child = ctx.GetChild(0);
            if (child is DDVParser.TypedArgumentContext)
                return VisitTypedArgument(child as DDVParser.TypedArgumentContext);
            else
                return VisitUnTypedArgument(child as DDVParser.UnTypedArgumentContext);
        }

        protected ParameterExpression VisitUnTypedArgument(DDVParser.UnTypedArgumentContext ctx)
        {
            return Expression.Parameter(typeof(object), ctx.GetText());
        }

        protected ParameterExpression VisitTypedArgument(DDVParser.TypedArgumentContext ctx)
        {
            var typeName = ctx.GetChild(0).GetText();
            var type = TypeHelper.GetType(typeName);
            return Expression.Parameter(type, ctx.GetChild(1).GetText());
        }

        protected bool TryVisitLinqMethod(DDVParser.FunctionContext ctx, out Expression exp)
        {
            var functionName = ctx.GetChild(0).GetText();
            exp = null;
            switch (functionName)
            {
                case "Aggregate":
                    {
                        exp = VisitLinqAggregate(ctx);
                        break;
                    }
                case "All":
                    {
                        exp = VisitLinqAll(ctx);
                        break;
                    }
                case "Any":
                    {
                        exp = VisitLinqAny(ctx);
                        break;
                    }
                case "Average":
                    {
                        exp = VisitLinqAverage(ctx);
                        break;
                    }
                case "Concat":
                    {
                        exp = VisitLinqConcat(ctx);
                        break;
                    }
                case "Contains":
                    {
                        exp = VisitLinqContains(ctx);
                        break;
                    }
                case "Count":
                    {
                        exp = VisitLinqCount(ctx);
                        break;
                    }
                case "Except":
                    {
                        exp = VisitLinqExcept(ctx);
                        break;
                    }
                case "First":
                    {
                        exp = VisitLinqFirst(ctx);
                        break;
                    }
                case "Intersect":
                    {
                        exp = VisitLinqIntersect(ctx);
                        break;
                    }
                case "Last":
                    {
                        exp = VisitLinqLast(ctx);
                        break;
                    }
                case "Max":
                    {
                        exp = VisitLinqMax(ctx);
                        break;
                    }
                case "Min":
                    {
                        exp = VisitLinqMin(ctx);
                        break;
                    }
                case "OrderBy":
                    {
                        exp = VisitLinqOrderBy(ctx);
                        break;
                    }
                case "OrderByDescending":
                    {
                        exp = VisitLinqOrderByDescending(ctx);
                        break;
                    }
                case "Reverse":
                    {
                        exp = VisitLinqReverse(ctx);
                        break;
                    }
                case "Select":
                    {
                        exp = VisitLinqSelect(ctx);
                        break;
                    }
                case "Single":
                    {
                        exp = VisitLinqSingle(ctx);
                        break;
                    }
                case "Skip":
                    {
                        exp = VisitLinqSkip(ctx);
                        break;
                    }
                case "SkipWhile":
                    {
                        exp = VisitLinqSkipWhile(ctx);
                        break;
                    }
                case "Sum":
                    {
                        exp = VisitLinqSum(ctx);
                        break;
                    }
                case "Take":
                    {
                        exp = VisitLinqTake(ctx);
                        break;
                    }
                case "ThenBy":
                    {
                        exp = VisitLinqThenBy(ctx);
                        break;
                    }
                case "ThenByDescending":
                    {
                        exp = VisitLinqThenByDescending(ctx);
                        break;
                    }
                case "Union":
                    {
                        exp = VisitLinqUnion(ctx);
                        break;
                    }
                case "Where":
                    {
                        exp = VisitLinqWhere(ctx);
                        break;
                    }
                case "Zip":
                    {
                        exp = VisitLinqZip(ctx);
                        break;
                    }
            }
            return exp != null;
        }

        protected virtual Expression VisitLinqZip(DDVParser.FunctionContext ctx)
        {
            throw new NotImplementedException();
        }

        protected virtual Expression VisitLinqWhere(DDVParser.FunctionContext ctx)
        {
            /* 
                function
                :	namedElement OpenParen CloseParen
                |	namedElement OpenParen parameter (Comma parameter)* CloseParen	
                ;

                parameter
                :	literal | propertyAccess | methodAccess | variable | lambdaVariable | arrayOfLiterals | arithmeticExpression | binaryExpression | function | ternaryFunction
                ;

                public static int Where<TSource>(this IQueryable<TSource> source, Expression<Func<TSource, bool>> predicate);
            */
            if (ctx.ChildCount == 4)
            {
                var functionName = ctx.GetChild(0).GetText();
                var elementType = ExpressionScope.Current.InstanceType.ResolveElementType();
                var linqMethod = typeof(Queryable).GetMethods(BindingFlags.Public | BindingFlags.Static)
                                                  .Single(mi => mi.Name.Equals(functionName)
                                                                && mi.GetParameters().Length == 2
                                                                && mi.GetParameters()[1].ParameterType.GetGenericArguments()[0].GetGenericArguments().Length == 2)
                                                  .MakeGenericMethod(elementType);

                var funcType = typeof(Func<,>).MakeGenericType(elementType, typeof(bool));

                VariableScope.Current.Add("0$", Expression.Parameter(elementType, "0$"));

                var funcBody = Visit(ctx.GetChild(2));

                var funcParameters = new ParameterExpression[]
                {
                    (ParameterExpression)VariableScope.Current["0$"]
                };

                var func = Expression.Quote(Expression.Lambda(funcType, funcBody, funcParameters));

                var linqCall = Expression.Call(null, linqMethod, AsQueryable(ExpressionScope.Current.Source), func);

                VariableScope.Current.Remove("0$");

                return linqCall;
            }

            return null;
        }

        protected virtual Expression VisitLinqUnion(DDVParser.FunctionContext ctx)
        {
            /* 
                function
                :	namedElement OpenParen CloseParen
                |	namedElement OpenParen parameter (Comma parameter)* CloseParen	
                ;

                parameter
                :	literal | propertyAccess | methodAccess | variable | lambdaVariable | arrayOfLiterals | arithmeticExpression | binaryExpression | function | ternaryFunction
                ;

                public static IQueryable<TSource> Union<TSource>(this IQueryable<TSource> source1, IEnumerable<TSource> source2);
           */
            if (ctx.ChildCount == 4)
            {
                var functionName = ctx.GetChild(0).GetText();
                var elementType = ExpressionScope.Current.InstanceType.ResolveElementType();
                var linqMethod = typeof(Queryable).GetMethods(BindingFlags.Public | BindingFlags.Static)
                                               .Single(mi => mi.Name.Equals(functionName) && mi.GetParameters().Length == 2)
                                               .MakeGenericMethod(elementType);

                var source2 = Visit(ctx.GetChild(2));
                var linqCall = Expression.Call(null, linqMethod, AsQueryable(ExpressionScope.Current.Source), Cast(source2, ExpressionScope.Current.Source));

                return linqCall;
            }

            return null;
        }

        protected virtual Expression VisitLinqThenByDescending(DDVParser.FunctionContext ctx)
        {
            /* 
                function
	            :	namedElement OpenParen CloseParen
	            |	namedElement OpenParen parameter (Comma parameter)* CloseParen	
	            ;

                parameter
	            :	literal | propertyAccess | methodAccess | variable | lambdaVariable | arrayOfLiterals | arithmeticExpression | binaryExpression | function | ternaryFunction
	            ;

                public static IOrderedQueryable<TSource> ThenByDescending<TSource, TKey>(this IOrderedQueryable<TSource> source, Expression<Func<TSource, TKey>> keySelector);
           */
            if (ctx.ChildCount == 4)
            {
                var functionName = ctx.GetChild(0).GetText();
                var elementType = ExpressionScope.Current.InstanceType.ResolveElementType();
                MethodInfo linqMethod;
                Expression linqCall = null;

                linqMethod = typeof(Queryable).GetMethods(BindingFlags.Public | BindingFlags.Static)
                                                .SingleOrDefault(mi =>
                                                    mi.Name.Equals(functionName)
                                                    && mi.GetParameters().Length == 2);

                VariableScope.Current.Add("0$", Expression.Parameter(elementType, "0$"));

                var funcBody = Visit(ctx.GetChild(2));

                linqMethod = linqMethod.MakeGenericMethod(elementType, funcBody.Type);

                var funcType = typeof(Func<,>).MakeGenericType(elementType, funcBody.Type);

                var funcParameters = new ParameterExpression[]
                {
                    (ParameterExpression)VariableScope.Current["0$"]
                };

                var func = Expression.Quote(Expression.Lambda(funcType, funcBody, funcParameters));

                linqCall = Expression.Call(null, linqMethod, AsQueryable(ExpressionScope.Current.Source), func);

                VariableScope.Current.Remove("0$");


                return linqCall;
            }

            return null;
        }

        protected virtual Expression VisitLinqThenBy(DDVParser.FunctionContext ctx)
        {
            /* 
                function
                :	namedElement OpenParen CloseParen
                |	namedElement OpenParen parameter (Comma parameter)* CloseParen	
                ;

                parameter
                :	literal | propertyAccess | methodAccess | variable | lambdaVariable | arrayOfLiterals | arithmeticExpression | binaryExpression | function | ternaryFunction
                ;

                public static IOrderedQueryable<TSource> ThenBy<TSource, TKey>(this IOrderedQueryable<TSource> source, Expression<Func<TSource, TKey>> keySelector);
            */
            if (ctx.ChildCount == 4)
            {
                var functionName = ctx.GetChild(0).GetText();
                var elementType = ExpressionScope.Current.InstanceType.ResolveElementType();
                MethodInfo linqMethod;
                Expression linqCall = null;

                linqMethod = typeof(Queryable).GetMethods(BindingFlags.Public | BindingFlags.Static)
                                                .SingleOrDefault(mi =>
                                                    mi.Name.Equals(functionName)
                                                    && mi.GetParameters().Length == 2);

                VariableScope.Current.Add("0$", Expression.Parameter(elementType, "0$"));

                var funcBody = Visit(ctx.GetChild(2));

                linqMethod = linqMethod.MakeGenericMethod(elementType, funcBody.Type);

                var funcType = typeof(Func<,>).MakeGenericType(elementType, funcBody.Type);

                var funcParameters = new ParameterExpression[]
                {
                    (ParameterExpression)VariableScope.Current["0$"]
                };

                var func = Expression.Quote(Expression.Lambda(funcType, funcBody, funcParameters));

                linqCall = Expression.Call(null, linqMethod, AsQueryable(ExpressionScope.Current.Source), func);

                VariableScope.Current.Remove("0$");


                return linqCall;
            }

            return null;
        }

        protected virtual Expression VisitLinqTake(DDVParser.FunctionContext ctx)
        {
            /* 
                function
                :	namedElement OpenParen CloseParen
                |	namedElement OpenParen parameter (Comma parameter)* CloseParen	
                ;

                parameter
                :	literal | propertyAccess | methodAccess | variable | lambdaVariable | arrayOfLiterals | arithmeticExpression | binaryExpression | function | ternaryFunction
                ;

                public static IQueryable<TSource> Take<TSource>(this IQueryable<TSource> source, int count);
            */
            if (ctx.ChildCount == 4)
            {
                var functionName = ctx.GetChild(0).GetText();
                var elementType = ExpressionScope.Current.InstanceType.ResolveElementType();
                MethodInfo linqMethod;
                Expression linqCall = null;

                linqMethod = typeof(Queryable).GetMethods(BindingFlags.Public | BindingFlags.Static)
                                                .SingleOrDefault(mi => mi.Name.Equals(functionName));

                VariableScope.Current.Add("0$", Expression.Parameter(elementType, "0$"));
                Expression funcBody;
                using (var scope = new ExpressionScope(typeof(int), "count", null, ExpressionScope.Current?.IsAssignment ?? false))
                {
                    funcBody = Visit(ctx.GetChild(2));
                }

                linqMethod = linqMethod.MakeGenericMethod(elementType);

                var funcParameters = new ParameterExpression[]
                {
                    (ParameterExpression)VariableScope.Current["0$"]
                };

                linqCall = Expression.Call(null, linqMethod, AsQueryable(ExpressionScope.Current.Source), funcBody);

                VariableScope.Current.Remove("0$");


                return linqCall;
            }

            return null;
        }

        protected virtual Expression VisitLinqSum(DDVParser.FunctionContext ctx)
        {
            /* 
                function
	            :	namedElement OpenParen CloseParen
	            |	namedElement OpenParen parameter (Comma parameter)* CloseParen	
	            ;

                parameter
	            :	literal | propertyAccess | methodAccess | variable | lambdaVariable | arrayOfLiterals | arithmeticExpression | binaryExpression | function | ternaryFunction
	            ;

                public static double Average(this IQueryable<int> source);
                public static double Average(this IQueryable<long> source);
                public static float Average(this IQueryable<float> source);
                public static double Average(this IQueryable<double> source);
                public static decimal Average(this IQueryable<decimal> source);
                public static double Average<TSource>(this IQueryable<TSource> source, Expression<Func<TSource, int>> selector);
                public static float Average<TSource>(this IQueryable<TSource> source, Expression<Func<TSource, float>> selector);
                public static double Average<TSource>(this IQueryable<TSource> source, Expression<Func<TSource, long>> selector);
                public static double Average<TSource>(this IQueryable<TSource> source, Expression<Func<TSource, double>> selector);
                public static decimal Average<TSource>(this IQueryable<TSource> source, Expression<Func<TSource, decimal>> selector);
           */
            if (ctx.ChildCount >= 3)
            {
                var functionName = ctx.GetChild(0).GetText();
                var elementType = ExpressionScope.Current.InstanceType.ResolveElementType();
                var argCount = ctx.ChildCount == 3 ? 1 : 2;
                MethodInfo linqMethod;
                Expression linqCall = null;
                if (argCount == 1)
                {
                    linqMethod = typeof(Queryable).GetMethods(BindingFlags.Public | BindingFlags.Static)
                                               .Single(mi =>
                                                        mi.Name.Equals(functionName)
                                                        && mi.GetParameters().Length == argCount
                                                        && mi.GetParameters()[0].ParameterType.GetGenericArguments()[0].Equals(elementType));
                    linqCall = Expression.Call(null, linqMethod, AsQueryable(ExpressionScope.Current.Source));
                }
                else if (ctx.GetChild(2) is DDVParser.ParameterContext && ctx.GetChild(2).GetChild(0) is DDVParser.PropertyAccessContext)
                {
                    linqMethod = typeof(Queryable).GetMethods(BindingFlags.Public | BindingFlags.Static)
                                               .SingleOrDefault(mi =>
                                                        mi.Name.Equals(functionName)
                                                        && mi.GetParameters().Length == argCount
                                                        && mi.GetParameters()[1].ParameterType.GetGenericArguments()[0].GetGenericArguments()[1].Equals(elementType));

                    VariableScope.Current.Add("0$", Expression.Parameter(elementType, "0$"));

                    var funcBody = Visit(ctx.GetChild(2));
                    if (linqMethod == null
                        && funcBody is MemberExpression
                        && ((MemberExpression)funcBody).Member.DeclaringType.Equals(typeof(PropertyInvoker)))
                    {
                        linqMethod = typeof(Invoker).GetMethod(functionName, BindingFlags.Public | BindingFlags.Static);
                    }

                    linqMethod = linqMethod.MakeGenericMethod(elementType);

                    var funcType = typeof(Func<,>).MakeGenericType(elementType, funcBody.Type);

                    var funcParameters = new ParameterExpression[]
                    {
                        (ParameterExpression)VariableScope.Current["0$"]
                    };

                    var func = Expression.Quote(Expression.Lambda(funcType, funcBody, funcParameters));

                    linqCall = Expression.Call(null, linqMethod, AsQueryable(ExpressionScope.Current.Source), func);

                    VariableScope.Current.Remove("0$");

                }
                return linqCall;
            }

            return null;
        }

        protected virtual Expression VisitLinqSkipWhile(DDVParser.FunctionContext ctx)
        {
            /* 
                function
                :	namedElement OpenParen CloseParen
                |	namedElement OpenParen parameter (Comma parameter)* CloseParen	
                ;

                parameter
                :	literal | propertyAccess | methodAccess | variable | lambdaVariable | arrayOfLiterals | arithmeticExpression | binaryExpression | function | ternaryFunction
                ;

                public static IQueryable<TSource> SkipWhile<TSource>(this IQueryable<TSource> source, Expression<Func<TSource, bool>> predicate);
           */
            if (ctx.ChildCount == 4)
            {
                var functionName = ctx.GetChild(0).GetText();
                var elementType = ExpressionScope.Current.InstanceType.ResolveElementType();
                var linqMethod = typeof(Queryable).GetMethods(BindingFlags.Public | BindingFlags.Static)
                                                  .Single(mi => mi.Name.Equals(functionName)
                                                                && mi.GetParameters().Length == 2
                                                                && mi.GetParameters()[1].ParameterType.GetGenericArguments()[0].GetGenericArguments().Length == 2)
                                                  .MakeGenericMethod(elementType);
                Expression linqCall;

                var funcType = typeof(Func<,>).MakeGenericType(elementType, typeof(bool));

                VariableScope.Current.Add("0$", Expression.Parameter(elementType, "0$"));

                var funcBody = Visit(ctx.GetChild(2));

                var funcParameters = new ParameterExpression[]
                {
                    (ParameterExpression)VariableScope.Current["0$"]
                };

                var func = Expression.Quote(Expression.Lambda(funcType, funcBody, funcParameters));

                linqCall = Expression.Call(null, linqMethod, AsQueryable(ExpressionScope.Current.Source), func);

                VariableScope.Current.Remove("0$");

                return linqCall;
            }

            return null;
        }

        protected virtual Expression VisitLinqSkip(DDVParser.FunctionContext ctx)
        {
            /* 
                function
                :	namedElement OpenParen CloseParen
                |	namedElement OpenParen parameter (Comma parameter)* CloseParen	
                ;

                parameter
                :	literal | propertyAccess | methodAccess | variable | lambdaVariable | arrayOfLiterals | arithmeticExpression | binaryExpression | function | ternaryFunction
                ;

                public static IQueryable<TSource> Skip<TSource>(this IQueryable<TSource> source, int count);
            */
            if (ctx.ChildCount == 4)
            {
                var functionName = ctx.GetChild(0).GetText();
                var elementType = ExpressionScope.Current.InstanceType.ResolveElementType();
                MethodInfo linqMethod;
                Expression linqCall = null;

                linqMethod = typeof(Queryable).GetMethods(BindingFlags.Public | BindingFlags.Static)
                                                .SingleOrDefault(mi => mi.Name.Equals(functionName));

                VariableScope.Current.Add("0$", Expression.Parameter(elementType, "0$"));
                Expression funcBody;
                using (var scope = new ExpressionScope(typeof(int), "count", null, ExpressionScope.Current?.IsAssignment ?? false))
                {
                    funcBody = Visit(ctx.GetChild(2));
                }

                linqMethod = linqMethod.MakeGenericMethod(elementType);

                var funcParameters = new ParameterExpression[]
                {
                    (ParameterExpression)VariableScope.Current["0$"]
                };

                linqCall = Expression.Call(null, linqMethod, AsQueryable(ExpressionScope.Current.Source), funcBody);

                VariableScope.Current.Remove("0$");


                return linqCall;
            }

            return null;
        }

        protected virtual Expression VisitLinqSingle(DDVParser.FunctionContext ctx)
        {
            /* 
                function
                :	namedElement OpenParen CloseParen
                |	namedElement OpenParen parameter (Comma parameter)* CloseParen	
                ;

                parameter
                :	literal | propertyAccess | methodAccess | variable | lambdaVariable | arrayOfLiterals | arithmeticExpression | binaryExpression | function | ternaryFunction
                ;

                public static int Single<TSource>(this IQueryable<TSource> source);
                public static int Single<TSource>(this IQueryable<TSource> source, Expression<Func<TSource, bool>> predicate);
           */
            if (ctx.ChildCount >= 3)
            {
                var functionName = ctx.GetChild(0).GetText();
                var elementType = ExpressionScope.Current.InstanceType.ResolveElementType();
                var argCount = ctx.ChildCount == 3 ? 1 : 2;
                var linqMethod = typeof(Queryable).GetMethods(BindingFlags.Public | BindingFlags.Static)
                                                  .Single(mi => mi.Name.Equals(functionName) && mi.GetParameters().Length == argCount)
                                                  .MakeGenericMethod(elementType);
                Expression linqCall;
                if (argCount == 1)
                {
                    linqCall = Expression.Call(null, linqMethod, AsQueryable(ExpressionScope.Current.Source));
                }
                else
                {
                    var funcType = typeof(Func<,>).MakeGenericType(elementType, typeof(bool));

                    VariableScope.Current.Add("0$", Expression.Parameter(elementType, "0$"));

                    var funcBody = Visit(ctx.GetChild(2));

                    var funcParameters = new ParameterExpression[]
                    {
                        (ParameterExpression)VariableScope.Current["0$"]
                    };

                    var func = Expression.Quote(Expression.Lambda(funcType, funcBody, funcParameters));

                    linqCall = Expression.Call(null, linqMethod, AsQueryable(ExpressionScope.Current.Source), func);

                    VariableScope.Current.Remove("0$");
                }
                return linqCall;
            }

            return null;
        }

        protected virtual Expression VisitLinqSelect(DDVParser.FunctionContext ctx)
        {
            /* 
                function
                :	namedElement OpenParen CloseParen
                |	namedElement OpenParen parameter (Comma parameter)* CloseParen	
                ;

                parameter
                :	literal | propertyAccess | methodAccess | variable | lambdaVariable | arrayOfLiterals 
                    | arithmeticExpression | binaryExpression | function | ternaryFunction| objectExpression | functionDefinition
                ;

                public static IQueryable<TResult> Select<TSource, TResult>(this IQueryable<TSource> source, Expression<Func<TSource, TResult>> selector);
            */
            if (ctx.ChildCount == 4)
            {
                var functionName = ctx.GetChild(0).GetText();
                var elementType = ExpressionScope.Current.InstanceType.ResolveElementType();
                MethodInfo linqMethod;
                Expression linqCall = null;

                linqMethod = typeof(Queryable).GetMethods(BindingFlags.Public | BindingFlags.Static)
                                                .SingleOrDefault(mi =>
                                                    mi.Name.Equals(functionName)
                                                    && mi.GetParameters().Length == 2 && mi.GetParameters()[1].ParameterType.GetGenericArguments()[0].GetGenericArguments().Length == 2);

                VariableScope.Current.Add("0$", Expression.Parameter(elementType, "0$"));

                var funcBody = Visit(ctx.GetChild(2));

                linqMethod = linqMethod.MakeGenericMethod(elementType, funcBody.Type);

                var funcType = typeof(Func<,>).MakeGenericType(elementType, funcBody.Type);

                var funcParameters = new ParameterExpression[]
                {
                    (ParameterExpression)VariableScope.Current["0$"]
                };

                var func = Expression.Quote(Expression.Lambda(funcType, funcBody, funcParameters));

                linqCall = Expression.Call(null, linqMethod, AsQueryable(ExpressionScope.Current.Source), func);

                VariableScope.Current.Remove("0$");


                return linqCall;
            }

            return null;
        }

        protected virtual Expression VisitLinqReverse(DDVParser.FunctionContext ctx)
        {
            /* 
                function
                :	namedElement OpenParen CloseParen
                |	namedElement OpenParen parameter (Comma parameter)* CloseParen	
                ;

                parameter
                :	literal | propertyAccess | methodAccess | variable | lambdaVariable | arrayOfLiterals | arithmeticExpression | binaryExpression | function | ternaryFunction
                ;

                public static int Reverse<TSource>(this IQueryable<TSource> source);
            */
            if (ctx.ChildCount == 3)
            {
                var functionName = ctx.GetChild(0).GetText();
                var elementType = ExpressionScope.Current.InstanceType.ResolveElementType();
                var linqMethod = typeof(Queryable).GetMethods(BindingFlags.Public | BindingFlags.Static)
                                               .Single(mi => mi.Name.Equals(functionName) && mi.GetParameters().Length == 1)
                                               .MakeGenericMethod(elementType);

                return Expression.Call(null, linqMethod, AsQueryable(ExpressionScope.Current.Source));
            }

            return null;
        }

        protected virtual Expression VisitLinqOrderByDescending(DDVParser.FunctionContext ctx)
        {
            /* 
                function
	            :	namedElement OpenParen CloseParen
	            |	namedElement OpenParen parameter (Comma parameter)* CloseParen	
	            ;

                parameter
	            :	literal | propertyAccess | methodAccess | variable | lambdaVariable | arrayOfLiterals | arithmeticExpression | binaryExpression | function | ternaryFunction
	            ;

                public static IOrderedQueryable<TSource> OrderByDescending<TSource, TKey>(this IQueryable<TSource> source, Expression<Func<TSource, TKey>> keySelector);
           */
            if (ctx.ChildCount == 4)
            {
                var functionName = ctx.GetChild(0).GetText();
                var elementType = ExpressionScope.Current.InstanceType.ResolveElementType();
                MethodInfo linqMethod;
                Expression linqCall = null;

                linqMethod = typeof(Queryable).GetMethods(BindingFlags.Public | BindingFlags.Static)
                                                .SingleOrDefault(mi =>
                                                    mi.Name.Equals(functionName)
                                                    && mi.GetParameters().Length == 2);

                VariableScope.Current.Add("0$", Expression.Parameter(elementType, "0$"));

                var funcBody = Visit(ctx.GetChild(2));

                linqMethod = linqMethod.MakeGenericMethod(elementType, funcBody.Type);

                var funcType = typeof(Func<,>).MakeGenericType(elementType, funcBody.Type);

                var funcParameters = new ParameterExpression[]
                {
                    (ParameterExpression)VariableScope.Current["0$"]
                };

                var func = Expression.Quote(Expression.Lambda(funcType, funcBody, funcParameters));

                linqCall = Expression.Call(null, linqMethod, AsQueryable(ExpressionScope.Current.Source), func);

                VariableScope.Current.Remove("0$");


                return linqCall;
            }

            return null;
        }

        protected virtual Expression VisitLinqOrderBy(DDVParser.FunctionContext ctx)
        {
            /* 
                function
	            :	namedElement OpenParen CloseParen
	            |	namedElement OpenParen parameter (Comma parameter)* CloseParen	
	            ;

                parameter
	            :	literal | propertyAccess | methodAccess | variable | lambdaVariable | arrayOfLiterals | arithmeticExpression | binaryExpression | function | ternaryFunction
	            ;

                public static IOrderedQueryable<TSource> OrderBy<TSource, TKey>(this IQueryable<TSource> source, Expression<Func<TSource, TKey>> keySelector);
           */
            if (ctx.ChildCount == 4)
            {
                var functionName = ctx.GetChild(0).GetText();
                var elementType = ExpressionScope.Current.InstanceType.ResolveElementType();
                MethodInfo linqMethod;
                Expression linqCall = null;

                linqMethod = typeof(Queryable).GetMethods(BindingFlags.Public | BindingFlags.Static)
                                                .SingleOrDefault(mi =>
                                                    mi.Name.Equals(functionName)
                                                    && mi.GetParameters().Length == 2);

                VariableScope.Current.Add("0$", Expression.Parameter(elementType, "0$"));

                var funcBody = Visit(ctx.GetChild(2));

                linqMethod = linqMethod.MakeGenericMethod(elementType, funcBody.Type);

                var funcType = typeof(Func<,>).MakeGenericType(elementType, funcBody.Type);

                var funcParameters = new ParameterExpression[]
                {
                    (ParameterExpression)VariableScope.Current["0$"]
                };

                var func = Expression.Quote(Expression.Lambda(funcType, funcBody, funcParameters));

                linqCall = Expression.Call(null, linqMethod, AsQueryable(ExpressionScope.Current.Source), func);

                VariableScope.Current.Remove("0$");


                return linqCall;
            }

            return null;
        }

        protected virtual Expression VisitLinqMin(DDVParser.FunctionContext ctx)
        {
            /* 
                function
	            :	namedElement OpenParen CloseParen
	            |	namedElement OpenParen parameter (Comma parameter)* CloseParen	
	            ;

                parameter
	            :	literal | propertyAccess | methodAccess | variable | lambdaVariable | arrayOfLiterals | arithmeticExpression | binaryExpression | function | ternaryFunction
	            ;

                public static double Max<TSource>(this IQueryable<TSource> source);
                public static double Max<TSource, TReslut>(this IQueryable<TSource> source, Expression<Func<TSource, TResult>> selector);
           */
            if (ctx.ChildCount >= 3)
            {
                var functionName = ctx.GetChild(0).GetText();
                var elementType = ExpressionScope.Current.InstanceType.ResolveElementType();
                var argCount = ctx.ChildCount == 3 ? 1 : 2;
                MethodInfo linqMethod;
                Expression linqCall = null;
                if (argCount == 1)
                {
                    linqMethod = typeof(Queryable).GetMethods(BindingFlags.Public | BindingFlags.Static)
                                                  .Single(mi =>
                                                        mi.Name.Equals(functionName)
                                                        && mi.GetParameters().Length == argCount)
                                                   .MakeGenericMethod(elementType);
                    linqCall = Expression.Call(null, linqMethod, AsQueryable(ExpressionScope.Current.Source));
                }
                else if (ctx.GetChild(2) is DDVParser.ParameterContext && ctx.GetChild(2).GetChild(0) is DDVParser.PropertyAccessContext)
                {
                    linqMethod = typeof(Queryable).GetMethods(BindingFlags.Public | BindingFlags.Static)
                                                  .SingleOrDefault(mi =>
                                                        mi.Name.Equals(functionName)
                                                        && mi.GetParameters().Length == argCount);

                    VariableScope.Current.Add("0$", Expression.Parameter(elementType, "0$"));

                    var funcBody = Visit(ctx.GetChild(2));
                    if (linqMethod == null
                        && funcBody is MemberExpression
                        && ((MemberExpression)funcBody).Member.DeclaringType.Equals(typeof(PropertyInvoker)))
                    {
                        linqMethod = typeof(Invoker).GetMethod("Max", BindingFlags.Public | BindingFlags.Static);
                    }

                    linqMethod = linqMethod.MakeGenericMethod(elementType, funcBody.Type);

                    var funcType = typeof(Func<,>).MakeGenericType(elementType, funcBody.Type);

                    var funcParameters = new ParameterExpression[]
                    {
                        (ParameterExpression)VariableScope.Current["0$"]
                    };

                    var func = Expression.Quote(Expression.Lambda(funcType, funcBody, funcParameters));

                    linqCall = Expression.Call(null, linqMethod, AsQueryable(ExpressionScope.Current.Source), func);

                    VariableScope.Current.Remove("0$");

                }
                return linqCall;
            }

            return null;
        }

        protected virtual Expression VisitLinqMax(DDVParser.FunctionContext ctx)
        {
            /* 
                function
	            :	namedElement OpenParen CloseParen
	            |	namedElement OpenParen parameter (Comma parameter)* CloseParen	
	            ;

                parameter
	            :	literal | propertyAccess | methodAccess | variable | lambdaVariable | arrayOfLiterals | arithmeticExpression | binaryExpression | function | ternaryFunction
	            ;

                public static double Max<TSource>(this IQueryable<TSource> source);
                public static double Max<TSource, TReslut>(this IQueryable<TSource> source, Expression<Func<TSource, TResult>> selector);
           */
            if (ctx.ChildCount >= 3)
            {
                var functionName = ctx.GetChild(0).GetText();
                var elementType = ExpressionScope.Current.InstanceType.ResolveElementType();
                var argCount = ctx.ChildCount == 3 ? 1 : 2;
                MethodInfo linqMethod;
                Expression linqCall = null;
                if (argCount == 1)
                {
                    linqMethod = typeof(Queryable).GetMethods(BindingFlags.Public | BindingFlags.Static)
                                                  .Single(mi =>
                                                        mi.Name.Equals(functionName)
                                                        && mi.GetParameters().Length == argCount)
                                                   .MakeGenericMethod(elementType);
                    linqCall = Expression.Call(null, linqMethod, AsQueryable(ExpressionScope.Current.Source));
                }
                else if (ctx.GetChild(2) is DDVParser.ParameterContext && ctx.GetChild(2).GetChild(0) is DDVParser.PropertyAccessContext)
                {
                    linqMethod = typeof(Queryable).GetMethods(BindingFlags.Public | BindingFlags.Static)
                                                  .SingleOrDefault(mi =>
                                                        mi.Name.Equals(functionName)
                                                        && mi.GetParameters().Length == argCount);

                    VariableScope.Current.Add("0$", Expression.Parameter(elementType, "0$"));

                    var funcBody = Visit(ctx.GetChild(2));
                    if (linqMethod == null
                        && funcBody is MemberExpression
                        && ((MemberExpression)funcBody).Member.DeclaringType.Equals(typeof(PropertyInvoker)))
                    {
                        linqMethod = typeof(Invoker).GetMethod("Max", BindingFlags.Public | BindingFlags.Static);
                    }

                    linqMethod = linqMethod.MakeGenericMethod(elementType, funcBody.Type);

                    var funcType = typeof(Func<,>).MakeGenericType(elementType, funcBody.Type);

                    var funcParameters = new ParameterExpression[]
                    {
                        (ParameterExpression)VariableScope.Current["0$"]
                    };

                    var func = Expression.Quote(Expression.Lambda(funcType, funcBody, funcParameters));

                    linqCall = Expression.Call(null, linqMethod, AsQueryable(ExpressionScope.Current.Source), func);

                    VariableScope.Current.Remove("0$");

                }
                return linqCall;
            }

            return null;
        }

        protected virtual Expression VisitLinqLast(DDVParser.FunctionContext ctx)
        {
            /* 
                function
                :	namedElement OpenParen CloseParen
                |	namedElement OpenParen parameter (Comma parameter)* CloseParen	
                ;

                parameter
                :	literal | propertyAccess | methodAccess | variable | lambdaVariable | arrayOfLiterals | arithmeticExpression | binaryExpression | function | ternaryFunction
                ;

                public static int Last<TSource>(this IQueryable<TSource> source);
                public static int Last<TSource>(this IQueryable<TSource> source, Expression<Func<TSource, bool>> predicate);
           */
            if (ctx.ChildCount >= 3)
            {
                var functionName = ctx.GetChild(0).GetText();
                var elementType = ExpressionScope.Current.InstanceType.ResolveElementType();
                var argCount = ctx.ChildCount == 3 ? 1 : 2;
                var linqMethod = typeof(Queryable).GetMethods(BindingFlags.Public | BindingFlags.Static)
                                               .Single(mi => mi.Name.Equals(functionName) && mi.GetParameters().Length == argCount)
                                               .MakeGenericMethod(elementType);
                Expression linqCall;
                if (argCount == 1)
                {
                    linqCall = Expression.Call(null, linqMethod, AsQueryable(ExpressionScope.Current.Source));
                }
                else
                {
                    var funcType = typeof(Func<,>).MakeGenericType(elementType, typeof(bool));

                    VariableScope.Current.Add("0$", Expression.Parameter(elementType, "0$"));

                    var funcBody = Visit(ctx.GetChild(2));
                    var funcParameters = new ParameterExpression[]
                    {
                        (ParameterExpression)VariableScope.Current["0$"]
                    };

                    var func = Expression.Quote(Expression.Lambda(funcType, funcBody, funcParameters));

                    linqCall = Expression.Call(null, linqMethod, AsQueryable(ExpressionScope.Current.Source), func);

                    VariableScope.Current.Remove("0$");
                }
                return linqCall;
            }

            return null;
        }

        protected virtual Expression VisitLinqIntersect(DDVParser.FunctionContext ctx)
        {
            /* 
                function
                :	namedElement OpenParen CloseParen
                |	namedElement OpenParen parameter (Comma parameter)* CloseParen	
                ;

                parameter
                :	literal | propertyAccess | methodAccess | variable | lambdaVariable | arrayOfLiterals | arithmeticExpression | binaryExpression | function | ternaryFunction
                ;

                public static IQueryable<TSource> Intersect<TSource>(this IQueryable<TSource> source1, IEnumerable<TSource> source2);
           */
            if (ctx.ChildCount == 4)
            {
                var functionName = ctx.GetChild(0).GetText();
                var elementType = ExpressionScope.Current.InstanceType.ResolveElementType();
                var linqMethod = typeof(Queryable).GetMethods(BindingFlags.Public | BindingFlags.Static)
                                               .Single(mi => mi.Name.Equals(functionName) && mi.GetParameters().Length == 2)
                                               .MakeGenericMethod(elementType);

                var source2 = Visit(ctx.GetChild(2));
                var linqCall = Expression.Call(null, linqMethod, AsQueryable(ExpressionScope.Current.Source), Cast(source2, ExpressionScope.Current.Source));

                return linqCall;
            }

            return null;
        }

        protected virtual Expression VisitLinqFirst(DDVParser.FunctionContext ctx)
        {
            /* 
                function
                :	namedElement OpenParen CloseParen
                |	namedElement OpenParen parameter (Comma parameter)* CloseParen	
                ;

                parameter
                :	literal | propertyAccess | methodAccess | variable | lambdaVariable | arrayOfLiterals | arithmeticExpression | binaryExpression | function | ternaryFunction
                ;

                public static int First<TSource>(this IQueryable<TSource> source);
                public static int First<TSource>(this IQueryable<TSource> source, Expression<Func<TSource, bool>> predicate);
           */
            if (ctx.ChildCount >= 3)
            {
                var functionName = ctx.GetChild(0).GetText();
                var elementType = ExpressionScope.Current.InstanceType.ResolveElementType();
                var argCount = ctx.ChildCount == 3 ? 1 : 2;
                var linqMethod = typeof(Queryable).GetMethods(BindingFlags.Public | BindingFlags.Static)
                                               .Single(mi => mi.Name.Equals(functionName) && mi.GetParameters().Length == argCount)
                                               .MakeGenericMethod(elementType);
                Expression linqCall;
                if (argCount == 1)
                {
                    linqCall = Expression.Call(null, linqMethod, AsQueryable(ExpressionScope.Current.Source));
                }
                else
                {
                    var funcType = typeof(Func<,>).MakeGenericType(elementType, typeof(bool));

                    VariableScope.Current.Add("0$", Expression.Parameter(elementType, "0$"));

                    var funcBody = Visit(ctx.GetChild(2));
                    var funcParameters = new ParameterExpression[]
                    {
                        (ParameterExpression)VariableScope.Current["0$"]
                    };

                    var func = Expression.Quote(Expression.Lambda(funcType, funcBody, funcParameters));

                    linqCall = Expression.Call(null, linqMethod, AsQueryable(ExpressionScope.Current.Source), func);

                    VariableScope.Current.Remove("0$");
                }
                return linqCall;
            }

            return null;
        }

        protected virtual Expression VisitLinqExcept(DDVParser.FunctionContext ctx)
        {
            /* 
                 function
                 :	namedElement OpenParen CloseParen
                 |	namedElement OpenParen parameter (Comma parameter)* CloseParen	
                 ;

                 parameter
                 :	literal | propertyAccess | methodAccess | variable | lambdaVariable | arrayOfLiterals | arithmeticExpression | binaryExpression | function | ternaryFunction
                 ;

                 public static IQueryable<TSource> Concat<TSource>(this IQueryable<TSource> source1, IEnumerable<TSource> source2);
            */
            if (ctx.ChildCount == 4)
            {
                var functionName = ctx.GetChild(0).GetText();
                var elementType = ExpressionScope.Current.InstanceType.ResolveElementType();
                var linqMethod = typeof(Queryable).GetMethods(BindingFlags.Public | BindingFlags.Static)
                                               .Single(mi => mi.Name.Equals(functionName) && mi.GetParameters().Length == 2)
                                               .MakeGenericMethod(elementType);

                var source2 = Visit(ctx.GetChild(2));
                var linqCall = Expression.Call(null, linqMethod, AsQueryable(ExpressionScope.Current.Source), Cast(source2, ExpressionScope.Current.Source));

                return linqCall;
            }

            return null;
        }

        protected virtual Expression VisitLinqCount(DDVParser.FunctionContext ctx)
        {
            /* 
                function
                :	namedElement OpenParen CloseParen
                |	namedElement OpenParen parameter (Comma parameter)* CloseParen	
                ;

                parameter
                :	literal | propertyAccess | methodAccess | variable | lambdaVariable | arrayOfLiterals | arithmeticExpression | binaryExpression | function | ternaryFunction
                ;

                public static int Count<TSource>(this IQueryable<TSource> source);
                public static int Count<TSource>(this IQueryable<TSource> source, Expression<Func<TSource, bool>> predicate);
           */
            if (ctx.ChildCount >= 3)
            {
                var functionName = ctx.GetChild(0).GetText();
                var elementType = ExpressionScope.Current.InstanceType.ResolveElementType();
                var argCount = ctx.ChildCount == 3 ? 1 : 2;
                var linqMethod = typeof(Queryable).GetMethods(BindingFlags.Public | BindingFlags.Static)
                                               .Single(mi => mi.Name.Equals(functionName) && mi.GetParameters().Length == argCount)
                                               .MakeGenericMethod(elementType);
                Expression linqCall;
                if (argCount == 1)
                {
                    linqCall = Expression.Call(null, linqMethod, AsQueryable(ExpressionScope.Current.Source));
                }
                else
                {
                    var funcType = typeof(Func<,>).MakeGenericType(elementType, typeof(bool));

                    VariableScope.Current.Add("0$", Expression.Parameter(elementType, "0$"));

                    var funcBody = Visit(ctx.GetChild(2));
                    var funcParameters = new ParameterExpression[]
                    {
                        (ParameterExpression)VariableScope.Current["0$"]
                    };

                    var func = Expression.Quote(Expression.Lambda(funcType, funcBody, funcParameters));

                    linqCall = Expression.Call(null, linqMethod, AsQueryable(ExpressionScope.Current.Source), func);

                    VariableScope.Current.Remove("0$");
                }
                return linqCall;
            }

            return null;
        }

        protected virtual Expression VisitLinqContains(DDVParser.FunctionContext ctx)
        {
            /* 
                function
                :	namedElement OpenParen CloseParen
                |	namedElement OpenParen parameter (Comma parameter)* CloseParen	
                ;

                parameter
                :	literal | propertyAccess | methodAccess | variable | lambdaVariable | arrayOfLiterals | arithmeticExpression | binaryExpression | function | ternaryFunction
                ;

                public static bool Contains<TSource>(this IQueryable<TSource> source, TSource item);
           */
            if (ctx.ChildCount == 4)
            {
                var functionName = ctx.GetChild(0).GetText();
                var elementType = ExpressionScope.Current.InstanceType.ResolveElementType();
                var linqMethod = typeof(Queryable).GetMethods(BindingFlags.Public | BindingFlags.Static)
                                               .Single(mi => mi.Name.Equals(functionName) && mi.GetParameters().Length == 2)
                                               .MakeGenericMethod(elementType);

                var item = Visit(ctx.GetChild(2));
                var linqCall = Expression.Call(null, linqMethod, AsQueryable(ExpressionScope.Current.Source), Cast(item, elementType));

                return linqCall;
            }

            return null;
        }

        protected virtual Expression VisitLinqConcat(DDVParser.FunctionContext ctx)
        {
            /* 
                 function
                 :	namedElement OpenParen CloseParen
                 |	namedElement OpenParen parameter (Comma parameter)* CloseParen	
                 ;

                 parameter
                 :	literal | propertyAccess | methodAccess | variable | lambdaVariable | arrayOfLiterals | arithmeticExpression | binaryExpression | function | ternaryFunction
                 ;

                 public static IQueryable<TSource> Concat<TSource>(this IQueryable<TSource> source1, IEnumerable<TSource> source2);
            */
            if (ctx.ChildCount == 4)
            {
                var functionName = ctx.GetChild(0).GetText();
                var elementType = ExpressionScope.Current.InstanceType.ResolveElementType();
                var linqMethod = typeof(Queryable).GetMethods(BindingFlags.Public | BindingFlags.Static)
                                               .Single(mi => mi.Name.Equals(functionName) && mi.GetParameters().Length == 2)
                                               .MakeGenericMethod(elementType);

                var source2 = Visit(ctx.GetChild(2));
                var linqCall = Expression.Call(null, linqMethod, AsQueryable(ExpressionScope.Current.Source), Cast(source2, ExpressionScope.Current.Source));

                return linqCall;
            }

            return null;
        }

        protected virtual Expression VisitLinqAverage(DDVParser.FunctionContext ctx)
        {
            /* 
                function
	            :	namedElement OpenParen CloseParen
	            |	namedElement OpenParen parameter (Comma parameter)* CloseParen	
	            ;

                parameter
	            :	literal | propertyAccess | methodAccess | variable | lambdaVariable | arrayOfLiterals | arithmeticExpression | binaryExpression | function | ternaryFunction
	            ;

                public static double Average(this IQueryable<int> source);
                public static double Average(this IQueryable<long> source);
                public static float Average(this IQueryable<float> source);
                public static double Average(this IQueryable<double> source);
                public static decimal Average(this IQueryable<decimal> source);
                public static double Average<TSource>(this IQueryable<TSource> source, Expression<Func<TSource, int>> selector);
                public static float Average<TSource>(this IQueryable<TSource> source, Expression<Func<TSource, float>> selector);
                public static double Average<TSource>(this IQueryable<TSource> source, Expression<Func<TSource, long>> selector);
                public static double Average<TSource>(this IQueryable<TSource> source, Expression<Func<TSource, double>> selector);
                public static decimal Average<TSource>(this IQueryable<TSource> source, Expression<Func<TSource, decimal>> selector);
           */
            if (ctx.ChildCount >= 3)
            {
                var functionName = ctx.GetChild(0).GetText();
                var elementType = ExpressionScope.Current.InstanceType.ResolveElementType();
                var argCount = ctx.ChildCount == 3 ? 1 : 2;
                MethodInfo linqMethod;
                Expression linqCall = null;
                if (argCount == 1)
                {
                    linqMethod = typeof(Queryable).GetMethods(BindingFlags.Public | BindingFlags.Static)
                                               .Single(mi =>
                                                        mi.Name.Equals(functionName)
                                                        && mi.GetParameters().Length == argCount
                                                        && mi.GetParameters()[0].ParameterType.GetGenericArguments()[0].Equals(elementType));
                    linqCall = Expression.Call(null, linqMethod, AsQueryable(ExpressionScope.Current.Source));
                }
                else if (ctx.GetChild(2) is DDVParser.ParameterContext && ctx.GetChild(2).GetChild(0) is DDVParser.PropertyAccessContext)
                {
                    linqMethod = typeof(Queryable).GetMethods(BindingFlags.Public | BindingFlags.Static)
                                               .SingleOrDefault(mi =>
                                                        mi.Name.Equals(functionName)
                                                        && mi.GetParameters().Length == argCount
                                                        && mi.GetParameters()[1].ParameterType.GetGenericArguments()[0].GetGenericArguments()[1].Equals(elementType));

                    VariableScope.Current.Add("0$", Expression.Parameter(elementType, "0$"));

                    var funcBody = Visit(ctx.GetChild(2));
                    if (linqMethod == null
                        && funcBody is MemberExpression
                        && ((MemberExpression)funcBody).Member.DeclaringType.Equals(typeof(PropertyInvoker)))
                    {
                        linqMethod = typeof(Invoker).GetMethod("Average", BindingFlags.Public | BindingFlags.Static);
                    }

                    linqMethod = linqMethod.MakeGenericMethod(elementType);

                    var funcType = typeof(Func<,>).MakeGenericType(elementType, funcBody.Type);

                    var funcParameters = new ParameterExpression[]
                    {
                        (ParameterExpression)VariableScope.Current["0$"]
                    };

                    var func = Expression.Quote(Expression.Lambda(funcType, funcBody, funcParameters));

                    linqCall = Expression.Call(null, linqMethod, AsQueryable(ExpressionScope.Current.Source), func);

                    VariableScope.Current.Remove("0$");

                }
                return linqCall;
            }

            return null;
        }

        protected virtual Expression VisitLinqAny(DDVParser.FunctionContext ctx)
        {
            /* 
                function
	            :	namedElement OpenParen CloseParen
	            |	namedElement OpenParen parameter (Comma parameter)* CloseParen	
	            ;

                parameter
	            :	literal | propertyAccess | methodAccess | variable | lambdaVariable | arrayOfLiterals | arithmeticExpression | binaryExpression | function | ternaryFunction
	            ;

                public static bool Any<TSource>(this IQueryable<TSource> source);
                public static bool Any<TSource>(this IQueryable<TSource> source, Expression<Func<TSource, bool>> predicate);
           */
            if (ctx.ChildCount >= 3)
            {
                var functionName = ctx.GetChild(0).GetText();
                var elementType = ExpressionScope.Current.InstanceType.ResolveElementType();
                var argCount = ctx.ChildCount == 3 ? 1 : 2;
                var linqMethod = typeof(Queryable).GetMethods(BindingFlags.Public | BindingFlags.Static)
                                               .Single(mi => mi.Name.Equals(functionName) && mi.GetParameters().Length == argCount)
                                               .MakeGenericMethod(elementType);
                Expression linqCall;
                if (argCount == 1)
                {
                    linqCall = Expression.Call(null, linqMethod, AsQueryable(ExpressionScope.Current.Source));
                }
                else
                {
                    var funcType = typeof(Func<,>).MakeGenericType(elementType, typeof(bool));

                    VariableScope.Current.Add("0$", Expression.Parameter(elementType, "0$"));

                    var funcBody = Visit(ctx.GetChild(2));
                    var funcParameters = new ParameterExpression[]
                    {
                        (ParameterExpression)VariableScope.Current["0$"]
                    };

                    var func = Expression.Quote(Expression.Lambda(funcType, funcBody, funcParameters));

                    linqCall = Expression.Call(null, linqMethod, AsQueryable(ExpressionScope.Current.Source), func);

                    VariableScope.Current.Remove("0$");
                }
                return linqCall;
            }

            return null;
        }

        protected virtual Expression VisitLinqAll(DDVParser.FunctionContext ctx)
        {
            /* 
                function
	            :	namedElement OpenParen CloseParen
	            |	namedElement OpenParen parameter (Comma parameter)* CloseParen	
	            ;

                parameter
	            :	literal | propertyAccess | methodAccess | variable | lambdaVariable | arrayOfLiterals | arithmeticExpression | binaryExpression | function | ternaryFunction
	            ;

                public static bool All<TSource>(this IQueryable<TSource> source, Expression<Func<TSource, bool>> predicate);
           */
            if (ctx.ChildCount == 4)
            {
                var functionName = ctx.GetChild(0).GetText();
                var elementType = ExpressionScope.Current.InstanceType.ResolveElementType();
                var linqMethod = typeof(Queryable).GetMethods(BindingFlags.Public | BindingFlags.Static)
                                               .Single(mi => mi.Name.Equals(functionName) && mi.GetParameters().Length == 2)
                                               .MakeGenericMethod(elementType);
                var funcType = typeof(Func<,>).MakeGenericType(elementType, typeof(bool));

                VariableScope.Current.Add("0$", Expression.Parameter(elementType, "0$"));

                var funcBody = Visit(ctx.GetChild(2));
                var funcParameters = new ParameterExpression[]
                {
                    (ParameterExpression)VariableScope.Current["0$"]
                };

                var func = Expression.Quote(Expression.Lambda(funcType, funcBody, funcParameters));

                var linqCall = Expression.Call(null, linqMethod, AsQueryable(ExpressionScope.Current.Source), func);

                VariableScope.Current.Remove("0$");

                return linqCall;
            }

            return null;
        }

        protected virtual Expression VisitLinqAggregate(DDVParser.FunctionContext ctx)
        {
            /* 
                function
	            :	namedElement OpenParen CloseParen
	            |	namedElement OpenParen parameter (Comma parameter)* CloseParen	
	            ;

                parameter
	            :	literal | propertyAccess | methodAccess | variable | lambdaVariable | arrayOfLiterals | arithmeticExpression | binaryExpression | function | ternaryFunction
	            ;

                public static TSource Aggregate<TSource>(this IQueryable<TSource> source, Expression<Func<TSource, TSource, TSource>> func);
           */
            if (ctx.ChildCount == 4)
            {
                var functionName = ctx.GetChild(0).GetText();
                var elementType = ExpressionScope.Current.InstanceType.ResolveElementType();
                var linqMethod = typeof(Queryable).GetMethods(BindingFlags.Public | BindingFlags.Static)
                                               .Single(mi => mi.Name.Equals(functionName) && mi.GetParameters().Length == 2)
                                               .MakeGenericMethod(elementType);
                var funcType = typeof(Func<,,>).MakeGenericType(elementType, elementType, elementType);

                VariableScope.Current.Add("0$", Expression.Parameter(elementType, "0$"));
                VariableScope.Current.Add("1$", Expression.Parameter(elementType, "1$"));

                var funcBody = Visit(ctx.GetChild(2));
                var funcParameters = new ParameterExpression[]
                {
                    (ParameterExpression)VariableScope.Current["0$"],
                    (ParameterExpression)VariableScope.Current["1$"]
                };

                var func = Expression.Quote(Expression.Lambda(funcType, funcBody, funcParameters));

                var linqCall = Expression.Call(null, linqMethod, AsQueryable(ExpressionScope.Current.Source), func);

                VariableScope.Current.Remove("0$");
                VariableScope.Current.Remove("1$");

                return linqCall;
            }

            return null;
        }

        protected virtual Expression AsQueryable(Expression source)
        {
            if (source.Type.Implements<IQueryable>()) return source;
            var asQueryableMethod = typeof(Queryable).GetMethods(BindingFlags.Public | BindingFlags.Static)
                                                     .First(mi => mi.Name.Equals("AsQueryable"))
                                                     .MakeGenericMethod(source.Type.ResolveElementType());
            return Expression.Call(
                null,
                asQueryableMethod,
                source);
        }

        protected virtual Expression OfType(Expression source, Expression target)
        {
            if (!source.Type.Implements(typeof(IEnumerable)))
                throw new NotSupportedException(string.Format("Cannot convert type '{0}' to IEnumerable<>", source.Type.Name));

            var ofTypeMethod = typeof(Enumerable).GetMethod("OfType", BindingFlags.Public | BindingFlags.Static)
                                           .MakeGenericMethod(target.Type.ResolveElementType());
            return Expression.Call(
                null,
                ofTypeMethod,
                source);
        }

        protected virtual Expression Cast(Expression source, Expression target)
        {
            return Cast(source, target.Type);
        }

        protected virtual Expression Cast(Expression source, Type targetType)
        {
            if (targetType.Implements(typeof(IEnumerable)))
            {
                var ofTypeMethod = typeof(Invoker).GetMethods(BindingFlags.Public | BindingFlags.Static)
                                                  .Single(mi => mi.Name.Equals("Cast") && mi.GetParameters()[0].ParameterType.Equals(typeof(IEnumerable)))
                                                  .MakeGenericMethod(targetType.ResolveElementType());
                return Expression.Call(
                    null,
                    ofTypeMethod,
                    source);
            }
            else
            {
                var ofTypeMethod = typeof(Invoker).GetMethods(BindingFlags.Public | BindingFlags.Static)
                                                  .Single(mi => mi.Name.Equals("Cast") && mi.GetParameters()[0].ParameterType.Equals(typeof(object)))
                                                  .MakeGenericMethod(targetType);
                return Expression.Call(
                    null,
                    ofTypeMethod,
                    source);
            }
        }

        protected virtual Expression VisitConcat(DDVParser.FunctionContext ctx)
        {
            var argumentCount = ctx.ChildCount - 3;
            argumentCount = argumentCount - (argumentCount / 2);
            var m = typeof(string).GetMethods(BindingFlags.Public | BindingFlags.Static)
                .Single(mi => mi.Name.Equals("Concat") && mi.GetParameters().Length == 2 && mi.GetParameters().All(p => p.ParameterType.Equals(typeof(string))));
            Expression concat = ToString(Visit(ctx.GetChild(2)));
            for (int i = 4; i < ctx.ChildCount; i += 2)
            {
                concat = Expression.Call(null, m, concat, ToString(Visit(ctx.GetChild(i))));
            }
            return concat;
        }

        protected virtual Expression VisitEmit(DDVParser.FunctionContext ctx)
        {
            var value = Convert(Visit(ctx.GetChild(2)), typeof(string));
            var pushFrame = Expression.Call(null,
                                            typeof(CodeContext).GetMethod("PushFrame", BindingFlags.Public | BindingFlags.Static),
                                            value,
                                            Expression.Constant(ctx.Start.Line),
                                            Expression.Constant(ConsoleColor.Cyan));
            var popFrame = Expression.Call(null,
                                           typeof(CodeContext).GetMethod("PopFrame", BindingFlags.Public | BindingFlags.Static),
                                           Expression.Constant(null, typeof(string)),
                                           Expression.Constant(ConsoleColor.Cyan));
            return Expression.TryCatchFinally(pushFrame, popFrame);
        }

        protected virtual Expression ToString(Expression expression)
        {
            return Expression.Call(expression,
                typeof(object).GetMethods(BindingFlags.Public | BindingFlags.Instance).Single(mi => mi.Name.Equals("ToString") && mi.GetParameters().Length == 0));
        }

        protected virtual Expression VisitGreaterThanOrEqualsFunction(DDVParser.FunctionContext ctx)
        {
            return Expression.GreaterThanOrEqual(Visit(ctx.GetChild(2)), Visit(ctx.GetChild(4)));
        }

        protected virtual Expression VisitGreaterThanFunction(DDVParser.FunctionContext ctx)
        {
            return Expression.GreaterThan(Visit(ctx.GetChild(2)), Visit(ctx.GetChild(4)));
        }

        protected virtual Expression VisitLessThanOrEqualsFunction(DDVParser.FunctionContext ctx)
        {
            return Expression.LessThanOrEqual(Visit(ctx.GetChild(2)), Visit(ctx.GetChild(4)));
        }

        protected virtual Expression VisitLessThanFunction(DDVParser.FunctionContext ctx)
        {
            return Expression.LessThan(Visit(ctx.GetChild(2)), Visit(ctx.GetChild(4)));
        }

        protected virtual Expression VisitQueryFunction(DDVParser.FunctionContext ctx)
        {
            /* public ModelList<T> Query<T>(string query, params string[] args) where T : IModel
                {
                    var qp = this.QueryProviderBuilder.CreateQueryProvider<T>();
                    var result = qp.Query(string.Format(query, args));

                    return new ModelList<T>(result.OfType<T>().ToList(), result.Offset, result.TotalRecords, result.PageCount, result.PageSize);
                }
            */

            IEnumerable<BQL.BQLError> errors;

            var argumentCount = ctx.ChildCount - 3;
            argumentCount = argumentCount - (argumentCount / 2);
            var args = new List<string>();
            for (int i = 4; i < ctx.ChildCount; i += 2)
            {
                args.Add("0");
            }

            var queryRaw = ctx.GetChild(2).GetString();

            // need to reformat the query string to double the { and replace 0$ with {0}
            queryRaw = queryRaw.Replace("{", "{{").Replace("}", "}}");
            var matches = Regex.Matches(queryRaw, @"(?<arg>\d\$)", RegexOptions.Multiline);
            var start = 0;
            foreach (var match in matches.Cast<Match>())
            {
                queryRaw = queryRaw.Substring(start, match.Index) + "{" + match.Groups["arg"].Value.Replace("$", "") + "}" + queryRaw.Substring(match.Index + 2, queryRaw.Length - match.Index - 2);
                start = match.Index + 2;
            }

            var query = string.Format(queryRaw, args.ToArray());

            if (BQL.Validate(query, out errors))
            {
                var bql = BQL.Parse(query);
                var pipeline = new JoinQueryPipeline<IAny>(bql);
                var root = pipeline.First();
                var returns = pipeline.Last() as JoinReturnQueryStep<IAny>;
                Type returnType = null;
                if (pipeline.OfType<JoinAggregateQueryStep<IAny>>().Count() == 1)
                {
                    // get the model type returned
                    var predicate = (root as JoinAggregateQueryStep<IAny>).Predicate as BQLParser.PredicateExpressionContext;
                    var vertex = predicate.GetChild(0) as BQLParser.VertexAccessorContext;
                    var entity = vertex.GetChild(0) as BQLParser.QualifiedElementContext;
                    var entityName = entity.GetText();
                    returnType = ModelTypeManager.GetModelType(entityName);
                    if (returns.ReturnType == ReturnType.Paths)
                    {
                        returnType = typeof(Path<>).MakeGenericType(returnType);
                    }
                }
                else if (pipeline.OfType<JoinAggregateQueryStep<IAny>>().Count() > 1 && returns.ReturnType == ReturnType.Nodes)
                {
                    returnType = typeof(IAny);
                }
                var queryMethod = typeof(IRuntime).GetMethod("Query").MakeGenericMethod(returnType);
                var argsExps = new List<Expression>();
                for (int i = 4; i < ctx.ChildCount; i += 2)
                {
                    argsExps.Add(ToString(Visit(ctx.GetChild(i))));
                }
                var callArgs = new List<Expression>();
                callArgs.Add(Expression.Constant(queryRaw));
                callArgs.Add(Expression.NewArrayInit(typeof(string), argsExps));
                return Expression.Call(Runtime, queryMethod, callArgs);
            }
            else
            {
                HandleError("Invalid BQL Query: '{0}'", query);
            }
            return null;
        }

        protected virtual Expression VisitPropertyAccess(DDVParser.PropertyAccessContext ctx)
        {
            /*
             property
	            :	(dot namedElement ('[' (literal | propertyAccess | methodAccess | arithmeticExpression | function) ']')? )+
	            ;

            propertyAccess
	            :	variable property?
                |	variable ('[' (literal | propertyAccess | methodAccess | arithmeticExpression | function) ']') property?
	            ;
            */
            var variable = Visit(ctx.GetChild(0));
            if (ctx.ChildCount == 1)
            {
                // just return the model property itself from the runtime
                return variable;
            }
            else if (ctx.ChildCount == 2 && ctx.GetChild(1) is DDVParser.PropertyContext)
            {
                using (var scope = new ExpressionScope(variable.Type, ctx.GetChild(0).GetText(), variable, ExpressionScope.Current?.IsAssignment ?? false))
                    return VisitProperty(variable, ctx.GetChild(1) as DDVParser.PropertyContext);
            }
            else if (ctx.GetChild(1).GetText() == "[")
            {
                Expression indexer = VisitArrayIndexer(variable, ctx.GetChild(2));

                if (indexer != null)
                {
                    if (ctx.ChildCount == 5)
                    {
                        using (var scope = new ExpressionScope(indexer.Type, ctx.GetChild(4).GetText(), indexer, ExpressionScope.Current?.IsAssignment ?? false))
                            return VisitProperty(indexer, ctx.GetChild(4) as DDVParser.PropertyContext);
                    }
                    else
                    {
                        return indexer;
                    }
                }
            }
            throw new NotSupportedException(string.Format("A parse tree child count of '{0}' is not for a property accessor.", ctx.ChildCount));
        }

        protected virtual Expression VisitArrayIndexer(Expression variable, IParseTree indexCtx)
        {
            Expression indexer = null;

            if (variable is MethodCallExpression && ((MethodCallExpression)variable).Method.DeclaringType.Equals(typeof(Invoker)))
            {
                variable = Convert(variable, typeof(IList));
            }
            else if (variable is MemberExpression && ((MemberExpression)variable).Expression.Type.Equals(typeof(PropertyInvoker)))
            {
                variable = Convert(variable, typeof(IList));
            }

            Expression index;
            using (var scope = new ExpressionScope(typeof(int), "index", variable, ExpressionScope.Current?.IsAssignment ?? false))
            {
                index = Visit(indexCtx);
            }

            if (variable.Type.IsGenericType
                && variable.Type.GetGenericTypeDefinition().Implements(typeof(IEnumerable<>))
                && !variable.Type.Implements<IList>())
            {
                var toArrayMethod = typeof(Enumerable).GetMethod("ToList", BindingFlags.Public | BindingFlags.Static)
                                                      .MakeGenericMethod(variable.Type.GetGenericArguments()[0]);
                variable = Expression.Call(null, toArrayMethod, variable);
            }

            if (variable.Type.IsArray)
            {
                var indexerType = typeof(int);
                index = Convert(index, indexerType);
                indexer = Expression.ArrayAccess(variable, index);
            }
            else if (variable.Type.Implements<IList>())
            {
                var itemAccessor = variable.Type.GetProperty("Item");
                var indexerType = itemAccessor.GetMethod.GetParameters()[0].ParameterType;
                if (indexerType.Equals(typeof(string)))
                {
                    index = ToString(index);
                }
                else if (index.Type != indexerType)
                {
                    index = Convert(index, indexerType);
                }
                indexer = Expression.Property(variable, "Item", index);
            }

            return indexer;
        }

        protected virtual Expression VisitProperty(Expression source, DDVParser.PropertyContext ctx)
        {
            /*
             property
	            :	(dot namedElement ('[' (literal | propertyAccess | methodAccess | arithmeticExpression | function) ']')? )+
	            ;
            */


            var currentSource = source;
            var i = 1;
            while (i < ctx.ChildCount)
            {
                var isTerminalProperty = IsTerminalProperty(ctx, i);
                var propertyName = ctx.GetChild(i).GetText();
                var sourceType = GetSourceType(currentSource);

                if (sourceType.Implements<IModel>() && propertyName.Equals("Owner") && !(ExpressionScope.Current?.IsAssignment ?? false))
                {
                    var methodInfo = typeof(IRuntime).GetMethod("GetOwner");
                    currentSource = Expression.Call(this.Runtime, methodInfo, currentSource);
                }
                else
                {
                    if (ExpressionScope.Current.InstanceName != "@@this" 
                        || (ExpressionScope.Current.Args as Dictionary<string, Expression>) == null
                        || !((Dictionary<string, Expression>)ExpressionScope.Current.Args).TryGetValue(propertyName, out currentSource))
                    {
                        currentSource = source;
                        var propertyInfo = sourceType.GetPublicProperty(propertyName);
                        if (propertyInfo == null)
                        {
                            // we'll try to resolve the property late-bound at run time by creating an instance of a late-bound
                            // property invoker that will receive an instance of the object source whose property requires runtime resolution
                            currentSource = Expression.New(
                                typeof(PropertyInvoker).GetConstructor(new Type[] { typeof(object), typeof(string) }),
                                currentSource,
                                Expression.Constant(propertyName));
                            propertyInfo = typeof(PropertyInvoker).GetProperty("Property");
                        }

                        currentSource = Expression.MakeMemberAccess(currentSource, propertyInfo);
                    }
                }
                if (i + 1 < ctx.ChildCount)
                {
                    if (ctx.GetChild(i + 1) is DDVParser.DotContext)
                    {
                        i += 2;
                    }
                    else if (ctx.GetChild(i + 1).GetText() == "[")
                    {
                        // we're in an array indexer
                        using (var scope = new ExpressionScope(typeof(int), "array", currentSource, false))
                            currentSource = VisitArrayIndexer(currentSource, ctx.GetChild(i + 2));
                        i += 5;
                    }
                }
                else break;
            }
            return currentSource;
        }

        protected bool IsTerminalProperty(DDVParser.PropertyContext ctx, int i)
        {
            return (i == ctx.ChildCount - 1
                || (ctx.GetChild(i + 1).GetText() == "[" && i + 4 >= ctx.ChildCount));
        }

        protected void VisitForEachIterator(DDVParser.ForEachIteratorContext context, out Expression collection, out ParameterExpression loopVar)
        {
            /*
                forEachIterator
	            :	variable In (propertyAccess | variable | arrayOfLiterals | methodAccess)
	            ;

            */

            collection = null;
            loopVar = null;
            var loopVarContext = context.GetChild(0);
            var loopVarName = loopVarContext.GetText();
            var collectionContext = context.GetChild(2);

            collection = Visit(collectionContext);
            using (var scope = new ExpressionScope(collection.Type.ResolveElementType(), loopVarName, collection, ExpressionScope.Current?.IsAssignment ?? false))
                loopVar = (ParameterExpression)Visit(loopVarContext);
        }

        protected Type GetSourceType(Expression exp)
        {
            if (exp is MemberExpression)
            {
                return ((PropertyInfo)((MemberExpression)exp).Member).PropertyType;
            }
            else if (exp is ConditionalExpression)
            {
                var trueExpression = ((ConditionalExpression)exp).IfTrue;
                return ((PropertyInfo)((MemberExpression)trueExpression).Member).PropertyType;
            }
            else if (exp is UnaryExpression)
            {
                return ((UnaryExpression)exp).Type;
            }
            else if (exp is ConstantExpression)
            {
                return ((ConstantExpression)exp).Type;
            }
            else if (exp is MethodCallExpression)
            {
                return ((MethodCallExpression)exp).Method.ReturnType;
            }
            else if (exp is BinaryExpression)
            {
                return ((BinaryExpression)exp).Type;
            }
            else if (exp is ParameterExpression)
            {
                return exp.Type;
            }
            else if (exp is IndexExpression)
            {
                return ((IndexExpression)exp).Type;
            }
            else if (exp is BlockExpression)
            {
                return ((BlockExpression)exp).Type;
            }
            throw new NotSupportedException(string.Format("The source type could be determined for an Expression of type '{0}'.", exp.GetType().Name));
        }

        public void Dispose()
        {
            VariableScope.Pop();
        }
    }
}

using Antlr4.Runtime;
using Antlr4.Runtime.Tree;
using Common;
using Common.Diagnostics;
using Data.Core.Evaluation;
using Data.Core.Security.Grammar;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Data.Core.Security
{
    public class RuleCompiler<T> : IRuleCompiler<T> where T : IRuntime
    {
        private ParameterExpression _runtimeParameter;

        public RuleCompiler()
        {

        }

        public bool Compile(IRule rule, out IEnumerable<ICompiledRule> compiledRules)
        {
            IEnumerable<ICompiledRule<T>> rules;
            var result = Compile(rule, out rules);
            if (result)
            {
                compiledRules = rules.OfType<ICompiledRule>().ToArray();
            }
            else
            {
                compiledRules = null;
            }
            return result;
        }

        public bool Compile(IRule rule, out IEnumerable<ICompiledRule<T>> compiledRules)
        {
            var success = true;
            compiledRules = new List<ICompiledRule<T>>();
            this.Rule = rule;

            foreach (var policy in rule.Policies)
            {
                this.Policy = policy;

                var modelTypes = policy.ModelTypes.Any(m => m.Equals("*"))
                    ? ModelTypeManager.ModelTypes
                    : policy.ModelTypes.Select(m => ModelTypeManager.GetModelType(m));

                foreach (var modelType in modelTypes)
                {
                    this.ModelType = modelType;

                    Func<T, bool> userEvaluator, modelEvaluator, appliesEvaluator;
                    if (!ValidateModelTypes(policy.ModelTypes)
                        || !CompileUserEvaluator(rule, policy, out userEvaluator)
                        || !CompileModelEvaluator(rule, policy, out modelEvaluator)
                        || !CompileAppliesEvaluator(rule, policy, out appliesEvaluator))
                    {
                        success = false;
                        ((List<ICompiledRule<T>>)compiledRules).Clear();
                        break;
                    }

                    ((List<ICompiledRule<T>>)compiledRules).Add(CreateCompiledRule(rule, policy, userEvaluator, modelEvaluator, appliesEvaluator));
                }
            }

            return success;
        }



        private bool ValidateModelTypes(string[] modelTypes)
        {
            Type type;
            return modelTypes.All(m => m.Equals("*") || ModelTypeManager.TryGetModelType(m, out type));
        }

        private ICompiledRule<T> CreateCompiledRule(IRule rule, IRulePolicy policy, Func<T, bool> userEvaluator, Func<T, bool> modelEvaluator, Func<T, bool> appliesEvaluator)
        {
            return new CompiledRule<T>(rule, policy, ModelType, userEvaluator, modelEvaluator, appliesEvaluator);
        }

        private bool CompileAppliesEvaluator(IRule rule, IRulePolicy policy, out Func<T, bool> evaluator)
        {
            evaluator = null;
            if (string.IsNullOrEmpty(policy.PolicySelector)
                || policy.PolicySelector == "*"
                || policy.PolicySelector == "'*'"
                || policy.PolicySelector.Equals("true", StringComparison.InvariantCultureIgnoreCase))
            {
                evaluator = (runtime) => true;
                return true;
            }
            else
            {
                ParserRuleContext ctx = null;
                IEnumerable<ACSL.ACSLError> errors;
                try
                {
                    if (ACSL.Validate(policy.PolicySelector, (parser) => { ctx = parser.logicalExpression(); return ctx; }, out errors))
                    {
                        // logical expression consisting of boolean tests to determine if the conditions evaluate to true or false
                        this._runtimeParameter = Expression.Parameter(typeof(T));
                        var expression = Visit(ctx);
                        var evaluatorExp = Expression.Lambda<Func<T, bool>>(expression, this._runtimeParameter);
                        evaluator = evaluatorExp.Compile();
                        return true;
                    }
                }
                catch (Exception ex)
                {
                    Logger.LogError(ex);
                    this.LastError = ex;
                }
            }
            return false;
        }

        private bool CompileUserEvaluator(IRule rule, IRulePolicy policy, out Func<T, bool> evaluator)
        {
            ParserRuleContext ctx = null;
            IEnumerable<ACSL.ACSLError> errors;
            evaluator = null;
            try
            {
                if (policy.UserEvaluator != "'*'" && ACSL.Validate(policy.UserEvaluator, (parser) => { ctx = parser.logicalExpression(); return ctx; }, out errors))
                {
                    // logical expression consisting of boolean tests to determine if the conditions evaluate to true or false
                    this._runtimeParameter = Expression.Parameter(typeof(T));
                    var expression = Visit(ctx);
                    var evaluatorExp = Expression.Lambda<Func<T, bool>>(expression, this._runtimeParameter);
                    evaluator = evaluatorExp.Compile();
                    return true;
                }
                else if (policy.UserEvaluator != "'*'" && ACSL.Validate(policy.UserEvaluator, (parser) => { ctx = parser.arrayOfLiterals(); return ctx; }, out errors))
                {
                    // an array of string values representing usernames or role names
                    string[] names;
                    if (Parse((ACSLParser.ArrayOfLiteralsContext)ctx, out names))
                    {
                        evaluator = (runtime) =>
                        {
                            return names.Any(value => value.Equals("*") || runtime.User.Username.Equals(value) || runtime.IsInRole(runtime.User, value));
                        };
                        return true;
                    }
                }
                else if (ACSL.Validate(policy.UserEvaluator, (parser) => { ctx = parser.stringLiteral(); return ctx; }, out errors))
                {
                    // a single username or role name or wildard character (*)
                    var value = ((ACSLParser.StringLiteralContext)ctx).GetString();

                    evaluator = (runtime) =>
                    {
                        return value.Equals("*") || runtime.User.Username.Equals(value) || runtime.IsInRole(runtime.User, value);
                    };
                    return true;
                }
            }
            catch (Exception ex)
            {
                Logger.LogError(ex);
                this.LastError = ex;
            }
            return false;
        }

        private bool CompileModelEvaluator(IRule rule, IRulePolicy policy, out Func<T, bool> evaluator)
        {
            ParserRuleContext ctx = null;
            IEnumerable<ACSL.ACSLError> errors;
            evaluator = null;
            try
            {
                if (policy.ModelEvaluator != "'*'" && ACSL.Validate(policy.ModelEvaluator, (parser) => { ctx = parser.logicalExpression(); return ctx; }, out errors))
                {
                    // logical expression consisting of boolean tests to determine if the conditions evaluate to true or false
                    this._runtimeParameter = Expression.Parameter(typeof(T));
                    var expression = Visit(ctx);
                    var evaluatorExp = Expression.Lambda<Func<T, bool>>(expression, this._runtimeParameter);
                    evaluator = evaluatorExp.Compile();
                    return true;
                }
                else if (policy.ModelEvaluator != "'*'" && ACSL.Validate(policy.ModelEvaluator, (parser) => { ctx = parser.arrayOfLiterals(); return ctx; }, out errors))
                {
                    // an array of string values representing model type names
                    string[] names;
                    if (Parse((ACSLParser.ArrayOfLiteralsContext)ctx, out names))
                    {
                        var types = names.Any(n => n.Equals("*"))
                                    ? new Type[] { typeof(IModel) }
                                    : names.Select(name => ModelTypeManager.GetModelType(name)).ToArray();
                        evaluator = (runtime) =>
                        {
                            return types.Any(type => runtime.Model.ModelType.Implements(type));
                        };
                        return true;
                    }
                }
                else if (ACSL.Validate(policy.ModelEvaluator, (parser) => { ctx = parser.stringLiteral(); return ctx; }, out errors))
                {
                    // a single model type or wildcard character (*)
                    var value = ((ACSLParser.StringLiteralContext)ctx).GetString();
                    var types = value.Equals("*")
                                    ? new Type[] { typeof(IModel) }
                                    : new Type[] { ModelTypeManager.GetModelType(value) };
                    evaluator = (runtime) =>
                    {
                        return types.Any(type => runtime.Model.ModelType.Implements(type));
                    };
                    return true;
                }
            }
            catch (Exception ex)
            {
                Logger.LogError(ex);
                this.LastError = ex;
            }
            return false;
        }

        private Expression Visit(IParseTree ctx)
        {
            if (ctx == null) return null;

            if (ctx is ACSLParser.LogicalExpressionContext)
            {
                return VisitLogicalExpression(ctx as ACSLParser.LogicalExpressionContext);
            }
            else if (ctx is ACSLParser.BooleanLiteralContext)
            {
                return VisitBooleanLiteral(ctx as ACSLParser.BooleanLiteralContext);
            }
            else if (ctx is ACSLParser.BinaryExpressionContext)
            {
                return VisitBinaryExpression(ctx as ACSLParser.BinaryExpressionContext);
            }
            else if (ctx is ACSLParser.FunctionContext)
            {
                return VisitFunction(ctx as ACSLParser.FunctionContext);
            }
            else if (ctx is ACSLParser.StringLiteralContext)
            {
                return VisitStringLiteral(ctx as ACSLParser.StringLiteralContext);
            }
            else if (ctx is ACSLParser.PropertyAccessContext)
            {
                return VisitPropertyAccess(ctx as ACSLParser.PropertyAccessContext);
            }
            else if (ctx is ACSLParser.ValidModelContext)
            {
                return VisitValidModel(ctx as ACSLParser.ValidModelContext);
            }
            else if (ctx is ACSLParser.LiteralContext)
            {
                return VisitLiteral(ctx as ACSLParser.LiteralContext);
            }
            else if (ctx is ACSLParser.ArithmeticExpressionContext)
            {
                return VisitArithmetic(ctx as ACSLParser.ArithmeticExpressionContext);
            }
            else if (ctx is ACSLParser.IntegerLiteralContext)
            {
                return VisitInteger(ctx as ACSLParser.IntegerLiteralContext);
            }
            else if (ctx is ACSLParser.FloatLiteralContext)
            {
                return VisitFloat(ctx as ACSLParser.FloatLiteralContext);
            }
            else if (ctx is ACSLParser.NullLiteralContext)
            {
                return VisitNull(ctx as ACSLParser.NullLiteralContext);
            }
            else if (ctx is ACSLParser.NumberLiteralContext)
            {
                return VisitNumber(ctx as ACSLParser.NumberLiteralContext);
            }
            else if (ctx is ACSLParser.ArithmeticOperatorContext)
            {
                return VisitArithemticOperator(ctx as ACSLParser.ArithmeticOperatorContext);
            }
            else if (ctx is ACSLParser.ArrayOfLiteralsContext)
            {
                return VisitArrayOfLiterals(ctx as ACSLParser.ArrayOfLiteralsContext);
            }

            throw new NotSupportedException(string.Format("The parse tree type '{0}' provided is not supported.", ctx.GetType().Name));
        }

        private Expression VisitArrayOfLiterals(ACSLParser.ArrayOfLiteralsContext ctx)
        {
            var count = ctx.ChildCount;
            var literals = new List<Expression>();
            for (int i = 1; i < count; i += 2)
            {
                literals.Add(Visit(ctx.GetChild(i)));
            }
            var elementType = GetSourceType(literals.First());
            if (literals.Count > 1)
            {
                foreach (var literal in literals.Skip(1))
                {
                    if (!GetSourceType(literal).Equals(elementType))
                    {
                        elementType = typeof(object);
                        for (int i = 0; i < literals.Count; i++)
                        {
                            literals[i] = Expression.Convert(literals[i], elementType);
                        }
                        break;
                    }
                }
            }
            return Expression.NewArrayInit(elementType, literals.ToArray());
        }

        private Expression VisitArithemticOperator(ACSLParser.ArithmeticOperatorContext ctx)
        {
            return Expression.Constant(ctx.GetText(), typeof(string));
        }

        private Expression VisitNumber(ACSLParser.NumberLiteralContext ctx)
        {
            return Visit(ctx.GetChild(0));
        }

        private class ArithmeticOperation
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

        private Expression VisitArithmetic(ACSLParser.ArithmeticExpressionContext ctx)
        {
            /* arithmeticExpression
	            :	(numberLiteral | propertyAccess) (arithmeticOperator (numberLiteral | propertyAccess))?
	            |	'(' arithmeticExpression ')'
	            ;
            */
            if (ctx.GetChild(1) is ACSLParser.ArithmeticExpressionContext)
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
                    // we want am even number of values in the list, so that our binary math expressions will always have a left and right side
                    // so we just add a + 0 to the end of the list
                    var ao = new ArithmeticOperation(Expression.Constant(Activator.CreateInstance(this.BinaryEqualityType), this.BinaryEqualityType), "+");
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

        private Expression VisitArithmeticOperation(ArithmeticOperation op)
        {
            switch (op.Operator)
            {
                case "*":
                    return Expression.Multiply(op.Value, op.Right.Value);
                case "/":
                    return Expression.Divide(op.Value, op.Right.Value);
                case "+":
                    return Expression.Add(op.Value, op.Right.Value);
                case "-":
                    return Expression.Subtract(op.Value, op.Right.Value);
                default:
                    throw new NotSupportedException(string.Format("An arithemtic operator of type '{0}' is not supported.", op.Operator));
            }
        }

        private int SortArithmeticOperators(ArithmeticOperation op1, ArithmeticOperation op2)
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

        private Expression VisitNull(ACSLParser.NullLiteralContext ctx)
        {
            if (this.BinaryEqualityType?.IsValueType ?? false)
            {
                throw new InvalidOperationException("A value type can never equal null.");
            }
            return Expression.Constant(null, BinaryEqualityType == null ? typeof(object) : this.BinaryEqualityType);
        }

        private Expression VisitFloat(ACSLParser.FloatLiteralContext ctx)
        {
            if (BinaryEqualityType == null || BinaryEqualityType.Equals(typeof(double)))
            {
                if (BinaryEqualityType == null)
                {
                    this.BinaryEqualityType = typeof(double);
                }
                return Expression.Constant(double.Parse(ctx.GetText()), this.BinaryEqualityType);
            }
            else if (BinaryEqualityType.Equals(typeof(float)))
            {
                return Expression.Constant(float.Parse(ctx.GetText()), this.BinaryEqualityType);
            }
            throw new NotSupportedException(string.Format("The current binary equality type of '{0}' is not a valid floating point type.", BinaryEqualityType.Name));
        }

        private Expression VisitInteger(ACSLParser.IntegerLiteralContext ctx)
        {
            if (BinaryEqualityType == null || BinaryEqualityType.Equals(typeof(long)))
            {
                if (BinaryEqualityType == null)
                {
                    this.BinaryEqualityType = typeof(long);
                }
                return Expression.Constant(long.Parse(ctx.GetText()), this.BinaryEqualityType);
            }
            else if (BinaryEqualityType.Equals(typeof(sbyte)))
            {
                return Expression.Constant(sbyte.Parse(ctx.GetText()), this.BinaryEqualityType);
            }
            else if (BinaryEqualityType.Equals(typeof(short)))
            {
                return Expression.Constant(short.Parse(ctx.GetText()), this.BinaryEqualityType);
            }
            else if (BinaryEqualityType.Equals(typeof(ushort)))
            {
                return Expression.Constant(ushort.Parse(ctx.GetText()), this.BinaryEqualityType);
            }
            else if (BinaryEqualityType.Equals(typeof(int)))
            {
                return Expression.Constant(int.Parse(ctx.GetText()), this.BinaryEqualityType);
            }
            else if (BinaryEqualityType.Equals(typeof(uint)))
            {
                return Expression.Constant(uint.Parse(ctx.GetText()), this.BinaryEqualityType);
            }
            else if (BinaryEqualityType.Equals(typeof(byte)))
            {
                return Expression.Constant(byte.Parse(ctx.GetText()), this.BinaryEqualityType);
            }
            else if (BinaryEqualityType.Equals(typeof(ulong)))
            {
                return Expression.Constant(ulong.Parse(ctx.GetText()), this.BinaryEqualityType);
            }
            throw new NotSupportedException(string.Format("The current binary equality type of '{0}' is not a valid integer type.", BinaryEqualityType.Name));
        }

        private Expression VisitLiteral(ACSLParser.LiteralContext ctx)
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

        private Expression VisitValidModel(ACSLParser.ValidModelContext ctx)
        {
            /* validModel
	            :	'@' namedElement
	            ;
            */

            var name = ctx.GetChild(1).GetText();
            if (name.Equals("model", StringComparison.InvariantCultureIgnoreCase))
            {
                BinaryEqualityType = ModelType;
            }
            return GetRuntimePropertyExpression(name);
        }

        private Expression GetRuntimePropertyExpression(string name)
        {
            var property = typeof(T).GetPublicProperties().FirstOrDefault(pi => pi.Name.Equals(name, StringComparison.InvariantCultureIgnoreCase));
            if (property == null)
            {
                throw new NotSupportedException(string.Format("A property named '{0}' could not be found on the security runtime api.", name));
            }
            var exp = (Expression)Expression.MakeMemberAccess(this._runtimeParameter, property);
            if (property.PropertyType.Equals(typeof(IModel)) && BinaryEqualityType != null)
            {
                exp = Expression.Convert(exp, BinaryEqualityType);
            }
            else
            {
                exp = Expression.Convert(exp, property.PropertyType);
            }
            return exp;
        }

        public IRule Rule { get; private set; }
        public IRulePolicy Policy { get; private set; }
        public Type ModelType { get; private set; }
        public Type BinaryEqualityType { get; private set; }
        public Exception LastError { get; private set; }

        private Expression VisitPropertyAccess(ACSLParser.PropertyAccessContext ctx)
        {
            /*
             property
	            :	(dot namedElement)+
	            ;

            propertyAccess
	            :	validModel property?
	            ;
            */
            var validModel = Visit(ctx.GetChild(0));
            if (ctx.ChildCount == 1)
            {
                // just return the model property itself from the runtime
                return validModel;
            }
            else if (ctx.ChildCount == 2)
            {
                return VisitProperty(validModel, ctx.GetChild(1) as ACSLParser.PropertyContext);
            }
            throw new NotSupportedException(string.Format("A parse tree child count of '{0}' is not for a property accessor.", ctx.ChildCount));
        }

        private Expression VisitProperty(Expression source, ACSLParser.PropertyContext ctx)
        {
            /*
             property
	            :	(dot namedElement)+
	            ;
            */


            var currentSource = source;

            for (int i = 1; i < ctx.ChildCount; i += 2)
            {
                // every other node with even nodes as Dot (.) characters, and odd nodes giving the property name as NamedElementContext
                var sourceType = GetSourceType(currentSource);
                var propertyName = ctx.GetChild(i).GetText();
                if (sourceType.Implements<IModel>() && propertyName.Equals("Owner"))
                {
                    var methodInfo = typeof(IRuntime).GetMethod("GetOwner");
                    currentSource = Expression.Call(_runtimeParameter, methodInfo, currentSource);
                }
                else
                {
                    var propertyInfo = sourceType.GetPublicProperty(propertyName);
                    if (propertyInfo == null)
                    {
                        throw new NotSupportedException(string.Format("The specified property of '{0}' could not be found on the current model type of '{1}'.", propertyName, ModelTypeManager.GetModelName(ModelType)));
                    }
                    else
                    {
                        currentSource = Expression.MakeMemberAccess(currentSource, propertyInfo);
                    }
                }
            }
            return currentSource;
        }

        private Type GetSourceType(Expression exp)
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
            throw new NotSupportedException(string.Format("The source type could be determined for an Expression of type '{0}'.", exp.GetType().Name));
        }

        private bool IsTypeOf<X>(Expression exp)
        {
            return GetSourceType(exp).IsTypeOrSubtypeOf<X>();
        }

        private Expression VisitStringLiteral(ACSLParser.StringLiteralContext ctx)
        {
            /* stringLiteral
                :	StringLiteral
                ;
            */
            return Expression.Constant(ctx.GetString());
        }


        private Expression VisitFunction(ACSLParser.FunctionContext ctx)
        {
            /* 
                function
	            :	namedElement '()'
	            |	namedElement'(' (literal | propertyAccess | validModel | arrayOfLiterals) (',' (literal | propertyAccess | validModel | arrayOfLiterals))* ')'
	            ;
            */
            var functionName = ctx.GetChild(0).GetText();
            MethodInfo method = null;
            var argExps = new List<Expression>();
            if (ctx.ChildCount == 2)
            {
                method = typeof(T).GetMethod(functionName, BindingFlags.Public | BindingFlags.Instance, null, Type.EmptyTypes, null);
            }
            else
            {
                var argumentCount = ctx.ChildCount - 3;
                argumentCount = argumentCount - (argumentCount / 2);
                var methods = typeof(T).GetMethods(BindingFlags.Public | BindingFlags.Instance)
                    .Where(mi => mi.Name.Equals(functionName) && mi.GetParameters().Length == argumentCount)
                    .ToArray();

                if (methods.Length == 0)
                {
                    // see if it's an edge method
                    var modelTypeName = ModelTypeManager.ModelFullNames.FirstOrDefault(mn => mn.Replace(".", "_").Equals(functionName, StringComparison.CurrentCultureIgnoreCase));
                    if (string.IsNullOrEmpty(modelTypeName))
                    {
                        throw new NotSupportedException(string.Format("The specified function '{0}' does not exist.", functionName));
                    }
                    var modelType = ModelTypeManager.GetModelType(modelTypeName);
                    if (modelType.Implements<INamedModel>())
                    {
                        methods = new MethodInfo[] { typeof(T).GetMethod("GetModel").MakeGenericMethod(modelType) };
                    }
                    else if (modelType.Implements<ILink>())
                    {
                        methods = new MethodInfo[] { typeof(T).GetMethod("EdgeExists").MakeGenericMethod(modelType) };
                    }
                    else
                    {
                        throw new NotSupportedException(string.Format("The specified function '{0}' does not exist.", functionName));
                    }
                }

                if (methods.Length > 1)
                {
                    throw new NotSupportedException(string.Format("Method overloading is not supported for method '{0}'.", functionName));
                }

                var args = new List<IParseTree>();
                for (int i = 2; i < ctx.ChildCount; i += 2)
                {
                    args.Add(ctx.GetChild(i));
                }

                method = methods.First();

                var parms = method.GetParameters();

                if (parms.Length != args.Count)
                {
                    throw new TargetParameterCountException(string.Format("The function '{0}' requires {1} paramters, but {2} were supplied.", functionName, parms.Length, args.Count));
                }

                for (int i = 0; i < parms.Length; i++)
                {
                    var parm = parms[i];
                    var arg = args[i];
                    this.BinaryEqualityType = parm.ParameterType;
                    argExps.Add(Visit(arg));
                }
            }

            return Expression.Call(this._runtimeParameter, method, argExps.ToArray());
        }

        private Expression VisitLogicalExpression(ACSLParser.LogicalExpressionContext ctx)
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
            else if (ctx.GetChild(1) is ACSLParser.LogicalOperatorContext)
            {
                var left = ctx.GetChild(0);
                var right = ctx.GetChild(2);
                var leftExp = Visit(left);
                var rightExp = Visit(right);
                return VisitLogicalOperator(leftExp, ctx.GetChild(1) as ACSLParser.LogicalOperatorContext, rightExp);
            }
            else if (ctx.GetChild(0) is TerminalNodeImpl && ((TerminalNodeImpl)ctx.GetChild(0)).GetText() == "(")
            {
                // '(' logicalExpression ')'
                return Visit(ctx.GetChild(1));
            }
            throw new NotSupportedException("The logical expression provided is not in a valid format");
        }

        private Expression VisitLogicalOperator(Expression left, ACSLParser.LogicalOperatorContext ctx, Expression right)
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

        private Expression VisitBooleanLiteral(ACSLParser.BooleanLiteralContext ctx)
        {
            /* booleanLiteral
                    :   'true'
                    |   'false'
                    ;
                */
            var value = bool.Parse(ctx.GetText());
            return Expression.Constant(value, typeof(bool));
        }

        private Expression VisitBinaryExpression(ACSLParser.BinaryExpressionContext ctx)
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
            else if (ctx.ChildCount == 3)
            {
                // (function | literal | propertyAccess | arithmeticExpression) booleanOperator (function | literal | propertyAccess | arithmeticExpression)
                this.BinaryEqualityType = null;
                var op = ctx.GetChild(1).GetText();
                var left = ctx.GetChild(0);
                var right = ctx.GetChild(2);
                Expression leftExp = null; //Visit(left);
                Expression rightExp = null; // Visit(right);
                if (left is ACSLParser.NullLiteralContext)
                {
                    rightExp = Visit(right);
                    BinaryEqualityType = rightExp.Type;
                    leftExp = Visit(left);
                }
                else
                {
                    leftExp = Visit(left);
                    BinaryEqualityType = leftExp.Type;
                    rightExp = Visit(right);
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
                                return Expression.Equal(leftExp, rightExp);
                            }
                        }
                    case ">":
                        {
                            return Expression.GreaterThan(leftExp, rightExp);
                        }
                    case "<":
                        {
                            return Expression.LessThan(leftExp, rightExp);
                        }
                    case ">=":
                        {
                            return Expression.GreaterThanOrEqual(leftExp, rightExp);
                        }
                    case "<=":
                        {
                            return Expression.LessThanOrEqual(leftExp, rightExp);
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
                                return Expression.NotEqual(leftExp, rightExp);
                            }
                        }
                    default:
                        throw new NotSupportedException("The equality operator provided is not supported.");
                }
            }
            else
                throw new NotSupportedException("The binary expression format provided is not supported.");
        }

        private bool Parse(ACSLParser.ArrayOfLiteralsContext ctx, out string[] names)
        {
            names = null;
            var list = new List<string>();
            for (int i = 1; i < ctx.ChildCount - 1; i++)
            {
                var child = ctx.children[i];
                if ((child is ACSLParser.StringLiteralContext))
                {
                    list.Add(((ACSLParser.StringLiteralContext)child).GetString());
                }
                else if ((child is ACSLParser.LiteralContext) && (child.GetChild(0) is ACSLParser.StringLiteralContext))
                {
                    list.Add(((ACSLParser.LiteralContext)child).GetChild<ACSLParser.StringLiteralContext>(0).GetString());
                }
                else if ((child is Antlr4.Runtime.Tree.TerminalNodeImpl) && ((Antlr4.Runtime.Tree.TerminalNodeImpl)child).GetText() == ",")
                {
                    continue;
                }
                else
                {
                    return false;
                }
            }
            names = list.ToArray();
            return true;
        }
    }

    public static class StringLiteralEx
    {
        public static string GetString(this ACSLParser.StringLiteralContext ctx)
        {
            var value = ctx.GetText();
            value = value.Substring(1, value.Length - 2).Replace("\\'", "'").Replace("\\\"", "\"");
            return value;
        }
    }
}

using Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Data.Core.Linq
{
    /// <summary>
    /// Builds an execution plan for a query expression
    /// </summary>
    public class ExecutionBuilder : ExpressionVisitor
    {
        QueryPolicy policy;
        QueryLinguist linguist;
        Expression executor;
        bool isTop = true;
        MemberInfo receivingMember;
        protected int nReaders = 0;
        List<ParameterExpression> variables = new List<ParameterExpression>();
        List<Expression> initializers = new List<Expression>();
        Dictionary<string, Expression> variableMap = new Dictionary<string, Expression>();

        protected ExecutionBuilder(QueryLinguist linguist, QueryPolicy policy, Expression executor)
        {
            this.linguist = linguist;
            this.policy = policy;
            this.executor = executor;
        }

        public static Expression Build(QueryLinguist linguist, QueryPolicy policy, Expression expression, Expression provider)
        {
            var executor = Expression.Parameter(typeof(QueryExecutor), "executor");
            var builder = new ExecutionBuilder(linguist, policy, executor);
            builder.variables.Add(executor);
            builder.initializers.Add(Expression.Call(Expression.Convert(provider, typeof(ICreateExecutor)), "CreateExecutor", null, null));
            var result = builder.Build(expression);
            return result;
        }

        protected Expression Build(Expression expression)
        {
            expression = this.Visit(expression);
            expression = this.AddVariables(expression);
            return expression;
        }

        protected List<ParameterExpression> Variables { get { return variables; } }
        protected MemberInfo ReceivingMember { get { return receivingMember; } }
        protected QueryPolicy Policy { get { return policy; } }
        protected int ReaderCount { get { return nReaders; } set { nReaders = value; } }
        protected Expression Executor { get { return executor; } }
        protected QueryLinguist Linguist { get { return linguist; } }

        protected List<Expression> Initializers { get { return initializers; } }

        private Expression AddVariables(Expression expression)
        {
            // add variable assignments up front
            if (this.variables.Count > 0)
            {
                List<Expression> exprs = new List<Expression>();
                for (int i = 0, n = this.variables.Count; i < n; i++)
                {
                    exprs.Add(MakeAssign(this.variables[i], this.initializers[i]));
                }
                exprs.Add(expression);
                Expression sequence = MakeSequence(exprs);  // yields last expression value

                // use invoke/lambda to create variables via parameters in scope
                Expression[] nulls = this.variables.Select(v => Expression.Constant(null, v.Type)).ToArray();
                expression = Expression.Invoke(Expression.Lambda(sequence, this.variables.ToArray()), nulls);
            }

            return expression;
        }

        private static Expression MakeAssign(ParameterExpression variable, Expression value)
        {
            return Expression.Call(typeof(ExecutionBuilder), "Assign", new Type[] { variable.Type }, variable, value);
        }

        public static T Assign<T>(ref T variable, T value)
        {
            variable = value;
            return value;
        }

        private static Expression MakeSequence(IList<Expression> expressions)
        {
            Expression last = expressions[expressions.Count - 1];
            expressions = expressions.Select(e => e.Type.IsValueType ? Expression.Convert(e, typeof(object)) : e).ToList();
            return Expression.Convert(Expression.Call(typeof(ExecutionBuilder), "Sequence", null, Expression.NewArrayInit(typeof(object), expressions)), last.Type);
        }

        public static object Sequence(params object[] values)
        {
            return values[values.Length - 1];
        }
    }
}

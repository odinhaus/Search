using Common;
using Data.Core.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;

namespace Data.Core.Linq.Win
{
    public class ClientExecutionBuilder : ExecutionBuilder
    {
        public ClientExecutionBuilder(QueryLinguist linguist, QueryPolicy policy, QueryResultBuilder typeBuilder, Expression executor)
            : base(linguist, policy, executor)
        { }

        public new static Expression Build(QueryLinguist linguist, QueryPolicy policy, Expression expression, Expression provider)
        {
            var executor = Expression.Parameter(typeof(QueryExecutor), "executor");
            var builder = new ClientExecutionBuilder(linguist, policy, new ClientQueryResultBuilder(linguist), executor);
            builder.Variables.Add(executor);
            builder.Initializers.Add(
                Expression.Convert(
                    Expression.Call(Expression.Convert(provider, typeof(ICreateExecutor)), "CreateExecutor", null, null),
                    typeof(QueryExecutor)));
            var result = builder.Build(expression);
            return result;
        }

        protected override Expression Visit(Expression exp)
        {
            if (exp == null) return null;
            switch((QueryExpressionType)exp.NodeType)
            {
                case QueryExpressionType.Predicate:
                    {
                        return BuildPredicateCommand((PredicateExpression)exp);
                    }
                case QueryExpressionType.Traverse:
                    {
                        return BuildExecuteTraverseCommand((TraverseExpression)exp);
                    }
                case QueryExpressionType.Save:
                    {
                        return BuildExecuteSaveCommand((SaveExpression)exp);
                    }
                case QueryExpressionType.Delete:
                    {
                        return BuildExecuteDeleteCommand((DeleteExpression)exp);
                    }
            }
            return base.Visit(exp);
        }

        private Expression BuildPredicateCommand(PredicateExpression exp)
        {
            /*

            public static ModelList<T> Find<T>(this IDataSet<T> source, Expression<Func<T, bool>> filterExpression = null) where T : IModel

            */
            var modelSet = exp.Body as ConstantExpression;
            var findMethod = typeof(Persistable).GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)
                                                .Single(mi => mi.GetParameters().Count() == 2 && mi.Name.Equals("Find"));
            var modelType = modelSet.Value.GetType().GetGenericArguments()[0];
            findMethod = findMethod.MakeGenericMethod(modelType);

            var keyProp = modelType.GetPublicProperties().Single(p => p.Name.Equals("Key"));
            var sortFunc = typeof(Expression<>).MakeGenericType(typeof(Func<,>).MakeGenericType(modelType, typeof(object)));
            var modelParam = Expression.Parameter(modelType);
            var keyRead = Expression.MakeMemberAccess(modelParam, keyProp);
            var predicate = Expression.Constant(null, typeof(Expression<>).MakeGenericType(typeof(Func<,>).MakeGenericType(modelType, typeof(bool))));
            var plan = Expression.Call(null, findMethod, modelSet, predicate);
            return plan; 
        }

        private Expression BuildExecuteDeleteCommand(DeleteExpression exp)
        {
            var commandText = this.Linguist.Format(exp);
            var method = ((ParameterExpression)this.Executor).Type.GetMethods().Single(mi => mi.Name.Equals("Delete") && mi.IsGenericMethod).MakeGenericMethod(exp.Type);
            var plan = Expression.Call(this.Executor, method,
                Expression.Constant(commandText));
            return plan;
        }

        private Expression BuildExecuteSaveCommand(SaveExpression exp)
        {
            var commandText = this.Linguist.Format(exp);
            var method = ((ParameterExpression)this.Executor).Type.GetMethods().Single(mi => mi.Name.Equals("Save") && mi.IsGenericMethod).MakeGenericMethod(exp.Type);
            var plan = Expression.Call(this.Executor, method,
                Expression.Constant(commandText));
            return plan;
        }

        private Expression BuildExecuteTraverseCommand(TraverseExpression exp)
        {
            var commandText = this.Linguist.Format(exp);
            var method = ((ParameterExpression)this.Executor).Type.GetMethods().Single(mi => mi.Name.Equals("Traverse") && mi.IsGenericMethod).MakeGenericMethod(exp.Origin.Type);
            var plan = Expression.Call(this.Executor, method,
                Expression.Constant(commandText));
            return plan;
        }

        Dictionary<string, Expression> variableMap = new Dictionary<string, Expression>();
        protected virtual Expression Parameterize(Expression expression)
        {
            return this.Linguist.Parameterize(expression);
        }
    }
}

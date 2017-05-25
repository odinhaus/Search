﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;

namespace Data.Core.Linq
{
    public class QueryCache : ICacheQueries
    {
        MostRecentlyUsedCache<QueryCompiler.CompiledQuery> cache;
        static readonly Func<QueryCompiler.CompiledQuery, QueryCompiler.CompiledQuery, bool> fnCompareQueries = CompareQueries;
        static readonly Func<object, object, bool> fnCompareValues = CompareConstantValues;
        int maxSize = 100;
        public QueryCache()
        {
            this.cache = new MostRecentlyUsedCache<QueryCompiler.CompiledQuery>(maxSize, fnCompareQueries);
        }

        private static bool CompareQueries(QueryCompiler.CompiledQuery x, QueryCompiler.CompiledQuery y)
        {
            return x.Policy.Equals(y.Policy) && ExpressionComparer.AreEqual(x.Query, y.Query, fnCompareValues);
        }

        private static bool CompareConstantValues(object x, object y)
        {
            if (x == y) return true;
            if (x == null || y == null) return false;
            if (x is IPersistable && y is IPersistable && x.GetType() == y.GetType()) return true;
            return object.Equals(x, y);
        }

        public object Execute(Expression query)
        {
            object[] args;
            var cached = this.Find(query, true, out args);
            return cached.Invoke(args);
        }

        public object Execute(IPersistable query)
        {
            return this.Equals(query.Expression);
        }

        public IEnumerable<T> Execute<T>(IPersistable<T> query)
        {
            return (IEnumerable<T>)this.Execute(query.Expression);
        }

        public int Count
        {
            get { return this.cache.Count; }
        }

        public void Clear()
        {
            this.cache.Clear();
        }

        public bool Contains(Expression query)
        {
            object[] args;
            return this.Find(query, false, out args) != null;
        }

        public bool Contains(IPersistable query)
        {
            return this.Contains(query.Expression);
        }

        private QueryCompiler.CompiledQuery Find(Expression query, bool add, out object[] args)
        {
            var pq = this.Parameterize(query, out args);
            var provider = this.FindProvider(pq);

            // add the provider instance into the args to be passed into the invocation
            args = args.Union(new[] { provider }).ToArray();
            // get the policy used for the query
            var policy = provider is IProvideRepository ? ((IProvideRepository)provider).Repository.Policy : null;
            // need to provide the policy instance used when constucting the query
            // as different policy settings can affect the query execution
            // this will allow the cache to store different versions of the same
            // basic query for different policies
            var cq = new QueryCompiler.CompiledQuery(pq, policy);
            QueryCompiler.CompiledQuery cached;
            this.cache.Lookup(cq, add, out cached);
            return cached;
        }

        private LambdaExpression Parameterize(Expression query, out object[] arguments)
        {
            IPersistableProvider provider = this.FindProvider(query);
            if (provider == null)
            {
                throw new ArgumentException("Cannot deduce query provider from query");
            }

            var ep = provider as IModelProvider;
            Func<Expression, bool> fn = ep != null ? (Func<Expression, bool>)ep.CanBeEvaluatedLocally : null;
            List<ParameterExpression> parameters = new List<ParameterExpression>();
            List<object> values = new List<object>();

            var body = PartialEvaluator.Eval(query, fn, c =>
            {
                bool isQueryRoot = c.Value is IPersistable;
                if (!isQueryRoot && ep != null && !ep.CanBeParameter(c))
                    return c;
                var p = Expression.Parameter(c.Type, "p" + parameters.Count);
                parameters.Add(p);
                values.Add(c.Value);
                // if query root then parameterize but don't replace in the tree 
                if (isQueryRoot)
                    return c;
                return p;
            });

            if (body.Type != typeof(object))
                body = Expression.Convert(body, typeof(object));

            arguments = values.ToArray();
            if (arguments.Length < 5)
            {
                return Expression.Lambda(body, parameters.ToArray());
            }
            else
            {
                arguments = new object[] { arguments };
                return ExplicitToObjectArray.Rewrite(body, parameters);
            }
        }

        private IPersistableProvider FindProvider(Expression expression)
        {
            ConstantExpression root = TypedSubtreeFinder.Find(expression, typeof(IPersistableProvider)) as ConstantExpression;
            if (root == null)
            {
                root = TypedSubtreeFinder.Find(expression, typeof(IPersistable)) as ConstantExpression;
            }
            if (root == null)
            {

            }
            if (root != null)
            {
                IPersistableProvider provider = root.Value as IPersistableProvider;
                if (provider == null)
                {
                    IPersistable query = root.Value as IPersistable;
                    if (query != null)
                    {
                        provider = query.Provider;
                    }
                }
                return provider;
            }
            return null;
        }

        public int MaxDepth
        {
            get { return this.maxSize; }
            set { this.maxSize = value; }
        }

        class ExplicitToObjectArray : ExpressionVisitor
        {
            IList<ParameterExpression> parameters;
            ParameterExpression array = Expression.Parameter(typeof(object[]), "array");

            private ExplicitToObjectArray(IList<ParameterExpression> parameters)
            {
                this.parameters = parameters;
            }

            internal static LambdaExpression Rewrite(Expression body, IList<ParameterExpression> parameters)
            {
                var visitor = new ExplicitToObjectArray(parameters);
                return Expression.Lambda(visitor.Visit(body), visitor.array);
            }

            protected override Expression VisitParameter(ParameterExpression p)
            {
                for (int i = 0, n = this.parameters.Count; i < n; i++)
                {
                    if (this.parameters[i] == p)
                    {
                        return Expression.Convert(Expression.ArrayIndex(this.array, Expression.Constant(i)), p.Type);
                    }
                }
                return p;
            }
        }
    }
}

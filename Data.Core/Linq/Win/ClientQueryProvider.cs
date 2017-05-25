using Common;
using Data.Core.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Data.Core.Linq.Win
{
    public class ClientQueryProvider : DataProvider, IExecuteQueries
    {
        public ClientQueryProvider(ITrackingRepository repository)
            : base(repository, new ClientQueryLanguage(), new ClientQueryMapping(), repository.Policy as QueryPolicy)
        {
            this.Repository = repository;
            this.Cache = repository.QueryCache;
        }

        protected override QueryExecutor CreateExecutor()
        {
            return new ClientQueryExecutor(this);
        }

        public override IPersistable CreateQuery(System.Linq.Expressions.Expression expression)
        {
            Type elementType = TypeHelper.GetElementType(expression.Type);
            try
            {
                if (elementType.Implements(typeof(IModel)))
                {
                    return (IPersistable)Activator.CreateInstance(typeof(ModelSet<>).MakeGenericType(elementType), new object[] { this, this.Repository, expression });
                }
                else if (elementType.Implements(typeof(ILink)))
                {
                    return (IPersistable)Activator.CreateInstance(typeof(LinkSet<>).MakeGenericType(elementType), new object[] { this, this.Repository, expression });
                }
                else return base.CreateQuery(expression);
            }
            catch (System.Reflection.TargetInvocationException tie)
            {
                throw tie.InnerException;
            }
        }

        public int Delete<T>(string query) where T : IModel
        {
            return new ClientQueryExecutor(this).Delete<T>(query);
        }

        public T Save<T>(string query) where T : IModel
        {
            return new ClientQueryExecutor(this).Save<T>(query);
        }

        public ModelList<T> Query<T>(string query) where T : IModel
        {
            return new ClientQueryExecutor(this).Query<T>(query);
        }
    }
}

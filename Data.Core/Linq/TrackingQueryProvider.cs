using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;

namespace Data.Core.Linq
{
    public class TrackingQueryProvider : DataProvider, IExecuteQueries
    {
        public TrackingQueryProvider(DataProvider provider)
            : base(provider.Repository, provider.Language, provider.Mapping, provider.Policy)
        {
            this.Provider = provider;
            this.Repository = provider.Repository;
        }

        public DataProvider Provider { get; private set; }

        protected override QueryExecutor CreateExecutor()
        {
            return new TrackingQueryExecutor((ITrackingRepository)this.Repository, ((ICreateExecutor)this.Provider).CreateExecutor());
        }
        public int Delete<T>(string query) where T : IModel
        {
            return this.CreateExecutor().Delete<T>(query);
        }

        public T Save<T>(string query) where T : IModel
        {
            return this.CreateExecutor().Save<T>(query);
        }

        public ModelList<T> Query<T>(string query) where T : IModel
        {
            return this.CreateExecutor().Query<T>(query);
        }
    }
}

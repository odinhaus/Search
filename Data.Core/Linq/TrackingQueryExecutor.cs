using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Data.Core.Linq
{
    /// <summary>
    /// Query executor that contains a non-tracking executor, and wraps the returned entity instances 
    /// attaching them to the current ITrackingRepository instance for which the queries are called.
    /// </summary>
    public class TrackingQueryExecutor : QueryExecutor
    {
        public TrackingQueryExecutor(ITrackingRepository repository, QueryExecutor executor)
        {
            this.Repository = repository;
            this.Executor = executor;
        }

        public QueryExecutor Executor { get; private set; }
        public ITrackingRepository Repository { get; private set; }


        public override int Delete<T>(string query)
        {
            return this.Executor.Delete<T>(query);
        }

        public override ModelList<T> Query<T>(string query)
        {
            return new TrackingModelList<T>(this.Executor.Query<T>(query), this.Repository);
        }

        public override T Save<T>(string query)
        {
            return this.Executor.Save<T>(query) ;
        }
    }
}

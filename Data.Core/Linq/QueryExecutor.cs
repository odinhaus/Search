using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Data.Core.Linq
{
    public abstract class QueryExecutor : IExecuteQueries
    {
        public abstract int Delete<T>(string query) where T : IModel;
        public abstract T Save<T>(string query) where T : IModel;
        public abstract ModelList<T> Query<T>(string query) where T : IModel;
    }
}

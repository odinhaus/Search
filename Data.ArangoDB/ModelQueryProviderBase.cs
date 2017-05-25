using Common.Diagnostics;
using Common.Security;
using Data.Core;
using Data.Core.Grammar;
using Data.Core.Web;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Data.ArangoDB
{
    public class ModelQueryProviderBase<T> : IModelQueryProvider<T> where T : IModel
    {
        [Override(typeof(QueryOverride))]
        public virtual ModelList<IAny> Query(string query)
        {
            var db = DbContext.Create();

            var pipeline = QueryBuilder.BuildJoinQueryPipeline<T>(query);

            return pipeline.Execute(db);
        }

        public object Raw(string query)
        {
            var db = DbContext.Create();
            return db.Query.Aql(query).ToDocuments();
        }

    }
}

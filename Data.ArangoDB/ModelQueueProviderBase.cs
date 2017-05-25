using Data.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Data.Core.Domains.Operations;
using Common.Diagnostics;
using Common.Linq;
using Common;
using Common.Security;
using Data.Core.Security;

namespace Data.ArangoDB
{
    public class ModelQueueProviderBase<T> : IModelQueueProvider<T> where T : IModel
    {
        public QueuedModel<T> Dequeue(IModelQueue queue)
        {
            var db = DbContext.Create();
            var results = new List<QueuedModel<T>>();

            var aql = QueryBuilder.BuildDequeueQuery<T>(queue);
#if (DEBUG)
            Logger.LogInfo(aql);
#endif

            var reads = new string[] { ModelCollectionManager.GetCollectionName<IUser>(), "Edge" };
            var writes = new string[] { "Edge" };
            var list = db.AqlTransacted(aql, writes, reads);

            if (list != null && list.Count == 1)
            {
                var docs = new List<Dictionary<string, object>>();
                docs.Add(list.First()["Model"] as Dictionary<string, object>);
                results.AddRange(new QueuedModelListBuilder<T>().Create(docs));

                var auditer = AppContext.Current.Container.GetInstance<IAuditer>();
                auditer.Audit(SecurityContext.Current?.CurrentPrincipal?.Identity ?? null, results.Select(r => r.Model).OfType<IModel>(), AuditEventType.Read);

                return results.FirstOrDefault();
            }

            return null;
        }

        public ModelList<QueuedModel<T>> Peek(IModelQueue queue, int offset = 0, int count = -1, bool includeItemsOnHold = false)
        {
            var db = DbContext.Create();
            var results = new List<QueuedModel<T>>();

            var aql = QueryBuilder.BuildPeekQueueQuery<T>(queue, offset, count, includeItemsOnHold);
#if (DEBUG)
            Logger.LogInfo(aql);
#endif
            var list = db.Query.Aql(aql).ToDocuments().Value;
            results.AddRange(new QueuedModelListBuilder<T>().Create(list));

            var totalCount = count > 0 ? QueuedCount(queue) : results.Count;
            var pageCount = count > 0 ? count : results.Count;
            count = count < 0 ? results.Count : count;

            var auditer = AppContext.Current.Container.GetInstance<IAuditer>();
            auditer.Audit(SecurityContext.Current?.CurrentPrincipal?.Identity ?? null, results.Select(r => r.Model).OfType<IModel>(), AuditEventType.Read);

            return new ModelList<QueuedModel<T>>(results, offset, totalCount, results.Count, count);
        }

        public int QueuedCount(IModelQueue queue)
        {
            var db = DbContext.Create();
            var count = 0;
            foreach (var query in queue.Queries)
            {
                var pipeline = QueryBuilder.BuildJoinQueryPipeline<T>(query.Query);
                var aql = QueryBuilder.BuildQueueCountQuery(pipeline.ToString());
#if (DEBUG)
                Logger.LogInfo(aql);
#endif
                var model = db.Query.Aql(aql).ToDocuments().Value.First();
                count += (int)(long)model["Count"];
            }

            var auditer = AppContext.Current.Container.GetInstance<IAuditer>();
            auditer.Audit(SecurityContext.Current?.CurrentPrincipal?.Identity ?? null, new IModel[] { queue }, AuditEventType.Read);

            return count;
        }


        public void Hold(IModelQueue queue, T item)
        {
            var db = DbContext.Create();
            var aql = QueryBuilder.BuildQueueHoldQuery<T>(queue, item);
#if (DEBUG)
            Logger.LogInfo(aql);
#endif

            var reads = new string[] { ModelCollectionManager.GetCollectionName(item.ModelType), ModelCollectionManager.GetCollectionName(queue.ModelType), "Edge" };
            var writes = new string[] { "Edge" };
            var result = db.AqlTransacted(aql, writes, reads);
        }

        public void Release(IModelQueue queue, T item)
        {
            var db = DbContext.Create();
            var aql = QueryBuilder.BuildQueueReleaseQuery<T>(queue, item);
#if (DEBUG)
            Logger.LogInfo(aql);
#endif

            var reads = new string[] { ModelCollectionManager.GetCollectionName(item.ModelType), ModelCollectionManager.GetCollectionName(queue.ModelType), "Edge" };
            var writes = new string[] { "Edge" };
            var result = db.AqlTransacted(aql, writes, reads);
        }
    }
}

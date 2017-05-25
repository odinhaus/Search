using Data.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Data.Core.Security;
using Common.Diagnostics;
using System.Security.Principal;
using Data.Core.Compilation;
using Data.Core.Auditing;

namespace Data.ArangoDB
{
    public class Auditer : IAuditer
    {
        public void Audit(IIdentity user, IEnumerable<IModel> models, AuditEventType eventType, string additionalData = null)
        {
            if (!AuditSettings.IsEnabled) return;

            var aql = QueryBuilder.BuildAuditInsertStatement(user, models, eventType, additionalData);
            var db = DbContext.Create();
#if (DEBUG)
            Logger.LogInfo(aql);
#endif
            var reads = new string[] { ModelCollectionManager.GetCollectionName<IUser>(), ModelCollectionManager.AuditCollection };
            var writes = new string[] { ModelCollectionManager.AuditCollection };

            var result = db.AqlTransacted(aql, writes, reads);

        }

        public void Audit<T>(IIdentity user, IEnumerable<T> models, AuditEventType eventType, string additionalData = null)
        {
            Audit(user, models.OfType<IModel>(), eventType, additionalData);
        }

        public ModelList<IAny> History(string globalModelKey, int offset, int pageSize)
        {
            var aql = QueryBuilder.BuildAuditHistoryQuery(globalModelKey, offset, pageSize);
            var db = DbContext.Create();
#if (DEBUG)
            Logger.LogInfo(aql);
#endif

            var result = db.Query.Aql(aql).ToDocuments();
            var list = new ModelListBuilder<IAny>().Create(
                result.Value, 
                offset, 
                (long)db.Query.Aql(QueryBuilder.BuildAuditHistoryTotalCountQuery(globalModelKey)).ToDocuments().Value[0]["Count"], 
                pageSize);

            for (int i = 0; i < list.PageCount; i++)
            {
                var link = list[i] as LinkBase;
                var _to = link._to.Replace("_", ".").Split('/');
                var _from = link._from.Replace("_", ".").Split('/');
                var toType = ModelTypeManager.GetModelType(link.TargetType);
                var toQuery = QueryBuilder.BuildGetQuery(toType, _to[1]);
                var fromType = ModelTypeManager.GetModelType(link.SourceType);
                var fromQuery = QueryBuilder.BuildGetQuery(fromType, _from[1]);
                var to = db.Query.Aql(toQuery).ToDocument().Value;
                link.To = (IModel)RuntimeModelBuilder.CreateModelInstanceActivator(toType)(to);
                var from = db.Query.Aql(fromQuery).ToDocument().Value;
                link.From = (IModel)RuntimeModelBuilder.CreateModelInstanceActivator(fromType)(from);
            }

            return list;
        }
    }
}

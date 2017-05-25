using Data.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Data.Core.Linq;
using System.Linq.Expressions;
using Common.Diagnostics;
using Common;
using Data.Core.Compilation;
using Common.Security;
using Data.Core.Security;

namespace Data.ArangoDB
{
    public class ModelListProviderBase<T> : IModelListProvider<T> where T : IModel
    {
        public virtual ModelList<T> Find(int offset = 0, int pageSize = 25, PredicateExpression filter = null, SortExpression[] sort = null)
        {
            var db = DbContext.Create();
            var query = QueryBuilder.BuildListQuery<T>(offset, pageSize, filter, sort);
#if (DEBUG)
            Logger.LogInfo(query);
#endif
            var result = db.Query.Aql(query).ToDocuments();
            var list = new ModelListBuilder<T>()
                .Create(result.Value, offset, (long)db.Query.Aql(QueryBuilder.BuildTotalCountQuery<T>(filter)).ToDocuments().Value[0]["Count"], pageSize);
            if (typeof(T).Implements<ILink>())
            {
                for(int i = 0; i < list.PageCount; i++)
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
                    link.From = (IModel)RuntimeModelBuilder.CreateModelInstanceActivator(fromType)(from); string failureMessage;
                    if (!DataAccessSecurityContext.Current.Demand(SecurityContext.Current.ToUser(), link as IModel, DataActions.Read, out failureMessage, null, null))
                    {
                        throw new System.Security.SecurityException(failureMessage);
                    }
                    if (!DataAccessSecurityContext.Current.Demand(SecurityContext.Current.ToUser(), link.From, DataActions.Read, out failureMessage, null, null))
                    {
                        throw new System.Security.SecurityException(failureMessage);
                    }
                    if (!DataAccessSecurityContext.Current.Demand(SecurityContext.Current.ToUser(), link.To, DataActions.Read, out failureMessage, null, null))
                    {
                        throw new System.Security.SecurityException(failureMessage);
                    }
                }
            }
            else
            {
                for (int i = 0; i < list.PageCount; i++)
                {
                    var item = list[i];
                    string failureMessage;
                    if (!DataAccessSecurityContext.Current.Demand(SecurityContext.Current.ToUser(), item, DataActions.Read, out failureMessage, null, null))
                    {
                        throw new System.Security.SecurityException(failureMessage);
                    }
                }
            }

            var auditer = AppContext.Current.Container.GetInstance<IAuditer>();
            auditer.Audit<T>(SecurityContext.Current.CurrentPrincipal.Identity, list, AuditEventType.Read);

            return list;
        }

        public virtual ModelList<T> Find(int offset = 0, int pageSize = 25, Expression<Func<T, bool>> filterExpression = null, SortField<T>[] sortExpressions = null)
        {
            var predicateExpression = (PredicateExpression)ListFilterVisitor.Convert(filterExpression);
            return Find(
                offset,
                pageSize,
                predicateExpression,
                sortExpressions?.Select(se => new SortExpression(se, SortDirection.Asc)).ToArray() ?? null);
        }
    }
}

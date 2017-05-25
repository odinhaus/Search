using Arango.Client;
using Common;
using Common.Diagnostics;
using Common.Security;
using Data.Core;
using Data.Core.Compilation;
using Data.Core.Grammar;
using Data.Core.Linq;
using Data.Core.Security;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Data.ArangoDB
{
    public class JoinQueryPipeline<T> : Core.Grammar.JoinQueryPipeline<T> where T : IModel
    {
        public JoinQueryPipeline(BQLParser.QueryExpressionContext bqlExpression) : base(bqlExpression) { }

        public ModelList<IAny> Execute(ADatabase database)
        {
            var query = QueryBuilder.BuildJoinQuery<T>(this);
#if (DEBUG)
            Logger.LogInfo(query);
#endif

            var offset = 0;
            var limit = Steps.SingleOrDefault(s => s is JoinLimitQueryStep<T>);
            if (limit != null)
            {
                offset = ((JoinLimitQueryStep<T>)limit).Offset;
            }
            var items = ExecuteQuery(database, query, offset);
            var auditer = AppContext.Current.Container.GetInstance<IAuditer>();
            auditer.Audit(SecurityContext.Current?.CurrentPrincipal?.Identity ?? null, items, AuditEventType.Read);
            return items;
        }

        private ModelList<IAny> ExecuteQuery(ADatabase database, string query, int offset)
        {
            var result = database.Query.Aql(query).ToDocuments();
            if (Steps.OfType<JoinReturnQueryStep<T>>().First().ReturnType == ReturnType.Nodes)
            {
                var list = new ModelListBuilder<IAny>()
                    .Create(result.Value, 0, result.Value?.Count??0, result.Value?.Count??0);
                if (typeof(T).Implements<ILink>())
                {
                    for (int i = 0; i < list.Count; i++)
                    {
                        var link = list[i] as LinkBase;
                        var _to = link._to.Replace("_", ".").Split('/');
                        var _from = link._from.Replace("_", ".").Split('/');
                        var toType = ModelTypeManager.GetModelType(link.TargetType);
                        var toQuery = QueryBuilder.BuildGetQuery(toType, _to[1]);
                        var fromType = ModelTypeManager.GetModelType(link.SourceType);
                        var fromQuery = QueryBuilder.BuildGetQuery(fromType, _from[1]);
                        var to = database.Query.Aql(toQuery).ToDocument().Value;
                        link.To = (IModel)RuntimeModelBuilder.CreateModelInstanceActivator(toType)(to);
                        var from = database.Query.Aql(fromQuery).ToDocument().Value;
                        link.From = (IModel)RuntimeModelBuilder.CreateModelInstanceActivator(fromType)(from);
                        string failureMessage;
                        if (DataAccessSecurityContext.Current.IsEnabled && !DataAccessSecurityContext.Current.Demand(SecurityContext.Current.ToUser(), link as IModel, DataActions.Read, out failureMessage, null, null))
                        {
                            throw new System.Security.SecurityException(failureMessage);
                        }
                        if (DataAccessSecurityContext.Current.IsEnabled && !DataAccessSecurityContext.Current.Demand(SecurityContext.Current.ToUser(), link.From, DataActions.Read, out failureMessage, null, null))
                        {
                            throw new System.Security.SecurityException(failureMessage);
                        }
                        if (DataAccessSecurityContext.Current.IsEnabled && !DataAccessSecurityContext.Current.Demand(SecurityContext.Current.ToUser(), link.To, DataActions.Read, out failureMessage, null, null))
                        {
                            throw new System.Security.SecurityException(failureMessage);
                        }
                    }
                }
                else
                {
                    for (int i = 0; i < list.Count; i++)
                    {
                        var item = list[i];
                        string failureMessage;
                        if (DataAccessSecurityContext.Current.IsEnabled && !DataAccessSecurityContext.Current.Demand(SecurityContext.Current.ToUser(), item, DataActions.Read, out failureMessage, null, null))
                        {
                            throw new System.Security.SecurityException(failureMessage);
                        }
                    }
                }

                return list;
            }
            else
            {
                var paths = new PathListBuilder<IAny>().Create(result.Value);
                foreach (var p in paths)
                {
                    var path = p as Path<IAny>;
                    foreach (var node in path.Nodes)
                    {
                        if (node == null) continue;
                        string failureMessage;
                        if (DataAccessSecurityContext.Current.IsEnabled && !DataAccessSecurityContext.Current.Demand(SecurityContext.Current.ToUser(), node, DataActions.Read, out failureMessage, null, null))
                        {
                            throw new System.Security.SecurityException(failureMessage);
                        }
                    }
                    foreach (var edge in path.Edges)
                    {
                        if (edge == null) continue;
                        string failureMessage;
                        if (DataAccessSecurityContext.Current.IsEnabled && !DataAccessSecurityContext.Current.Demand(SecurityContext.Current.ToUser(), edge, DataActions.Read, out failureMessage, null, null))
                        {
                            throw new System.Security.SecurityException(failureMessage);
                        }
                    }
                }
                return paths;
            }
        }

        public override string ToString()
        {
            return QueryBuilder.BuildJoinQuery<T>(this);
        }
    }
}

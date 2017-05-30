using Common;
using Common.Serialization;
using Data.ArangoDB.Linq;
using Data.Core;
using Data.Core.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Data.Core.Grammar;
using System.Collections;
using Arango.Client;
using Antlr4.Runtime.Tree;
using Data.Core.Domains.Operations;
using Common.Security;
using Data.Core.Security;
using System.Security.Principal;

namespace Data.ArangoDB
{
    public class QueryBuilder
    {
        public static string BuildListQuery<T>(long offset, long pageSize, PredicateExpression predicate, params SortExpression[] sortExpressions) where T : IModel
        {
            return BuildQueryStatement<T>(offset, pageSize, predicate, sortExpressions);
        }

        public static string BuildTotalCountQuery<T>(PredicateExpression predicate) where T :IModel
        {
            return string.Format("FOR {0} IN {1} {2} COLLECT WITH COUNT INTO length RETURN {{Count: length}}", GetItemName<T>(), ModelCollectionManager.GetCollectionName<T>(), BuildPredicateStatement<T>(predicate));
        }


        public static string BuildQueueCountQuery(string query)
        {
            return query.Replace("RETURN r","") + " COLLECT WITH COUNT INTO length RETURN {Count: length}";
        }

        public static string BuildAuditHistoryQuery(string globalModelKey, int offset, int pageSize)
        {
            var template = @"LET scopes = 
(
    FOR auditevent in {1} 
    FILTER auditevent._to == ""{0}"" 
    RETURN DISTINCT auditevent.ScopeId
)
FOR scope in scopes
FOR e in {1}
FILTER e.ScopeId == scope
SORT e.Created DESC
LIMIT {2}, {3}
RETURN e";

            return string.Format(template, ModelCollectionManager.GetIdFromGlobalKey(globalModelKey), ModelCollectionManager.AuditCollection, offset, pageSize);
        }


        public static string BuildAuditHistoryTotalCountQuery(string globalModelKey)
        {
            var template = @"LET scopes = 
(
    FOR auditevent in {1} 
    FILTER auditevent._to == ""{0}"" 
    RETURN DISTINCT auditevent.ScopeId
)
FOR scope in scopes
FOR e in {1}
FILTER e.ScopeId == scope
COLLECT WITH COUNT INTO length RETURN {{Count: length}}";
            return string.Format(template,
                ModelCollectionManager.GetIdFromGlobalKey(globalModelKey),
                ModelCollectionManager.AuditCollection);
        }


        public static JoinQueryPipeline<T> BuildJoinQueryPipeline<T>(string query) where T : IModel
        {
            var bqlExpression = BQL.Parse(query);
            return new JoinQueryPipeline<T>(bqlExpression);
        }

        public static string BuildJoinQuery<T>(JoinQueryPipeline<T> joinQueryPipeline) where T : IModel
        {
            var bqlExpressions = joinQueryPipeline.OfType<JoinAggregateQueryStep<T>>()
                                                        .Select(step => BqlTreeConverter.Convert(step.Predicate))
                                                        .ToArray();
            var sb = new StringBuilder();
            var varIdx = 0;
            var returns = joinQueryPipeline.OfType<JoinReturnQueryStep<T>>().FirstOrDefault();

            foreach (var exp in bqlExpressions)
            {
                sb.AppendLine(string.Format("LET q{0} = ({1})", varIdx++, BuildJoinStatement<T>(exp, returns)));
            }

            if (bqlExpressions.Count() > 1)
            {
                var statement = "";
                BuildJoinAggregateStatement<T>(1, joinQueryPipeline.OfType<JoinAggregateQueryStep<T>>().ToArray(), ref statement);
                sb.AppendLine("FOR r in " + statement);
            }
            else
            {
                sb.AppendLine("FOR r in q0");
            }

            var sortExpression = joinQueryPipeline.OfType<JoinSortQueryStep<T>>().FirstOrDefault();
            if (sortExpression != null)
            {
                sb.AppendLine(BuildJoinSortStatement(sortExpression));
            }

            var limitExpression = joinQueryPipeline.OfType<JoinLimitQueryStep<T>>().FirstOrDefault();
            if (limitExpression != null)
            {
                sb.Append(BuildJoinLimitStatement(limitExpression));
            }

            sb.AppendLine("RETURN r");
            return sb.ToString();
        }

        public static string BuildQueueHoldQuery<T>(IModelQueue queue, T item) where T : IModel
        {
            return BuildQueueHoldReleaseQuery(queue, item, true);
        }

        public static string BuildQueueReleaseQuery<T>(IModelQueue queue, T item) where T : IModel
        {
            return BuildQueueHoldReleaseQuery(queue, item, false);
        }

        private static string BuildQueueHoldReleaseQuery<T>(IModelQueue queue, T item, bool isOnHold) where T : IModel
        {
            //ModelSecurityManager.Demand(queue.ModelType, DataActions.Read);
            //ModelSecurityManager.Demand(item.ModelType, DataActions.Read);
            //ModelSecurityManager.Demand<queueOverrides>(DataActions.Update | DataActions.Create);
            var template = @"
    LET o = (
    FOR queue in " + ModelCollectionManager.GetCollectionName(queue.ModelType) + @"
    FOR override in " + ModelCollectionManager.EdgeCollection + @"
    FILTER queue._key == ""{0}"" && override._from == ""{1}"" && override._to == ""{2}""
    RETURN override
    )
    LET u = LENGTH(o) == 0?[{{_key: null}}] : [{{_key: FIRST(o)._key}}]
    FOR i in u
    UPSERT
    {{ _key: i._key }}
    INSERT 
        {{ 
            _from: ""{1}"", 
            _to: ""{2}"", 
            ModelType: ""{3}"", 
            IsDeleted: false, 
            TargetType: ""{4}"", 
            SourceType: ""{5}"", 
            ForcedRank: 0, 
            IsOnHold: {6}, 
            Created: DATE_ISO8601(DATE_NOW()),
            Modified: DATE_ISO8601(DATE_NOW())
        }}
    UPDATE {{ IsOnHold: {6}, Modified: DATE_ISO8601(DATE_NOW()) }}
    In " + ModelCollectionManager.EdgeCollection + @"
    RETURN NEW";
            var query = string.Format(template,
                queue.Key,
                queue.Id(),
                item.Id(),
                ModelTypeManager.GetModelName(typeof(queueOverrides)),
                ModelTypeManager.GetModelName(item.ModelType),
                ModelTypeManager.GetModelName(queue.ModelType),
                isOnHold.ToString()
                );
            return query;
        }

        public static string BuildDequeueQuery<T>(IModelQueue queue) where T : IModel
        {
            //ModelSecurityManager.Demand(queue.ModelType, DataActions.Read);
            //ModelSecurityManager.Demand<queueOverrides>(DataActions.Update | DataActions.Create);
            var peek = BuildPeekQueueQuery<T>(queue);
            var template = @"
    LET locks = (
        FOR lock in " + ModelCollectionManager.EdgeCollection + @"
        FOR user in {4}
        FILTER 
        (
            lock.ModelType == ""{2}"" &&
            lock._to == i._id &&
            lock._from == user._id &&
            lock.Expires > DATE_ISO8601(DATE_NOW()) &&
            lock.IsDeleted == false
        )
        RETURN {{ ""lock"": lock, ""user"": user}}
    )
    LET user = FIRST(
        FOR u in {4}
        FILTER u.Username == ""{0}""
        RETURN u
    )
    LET results = (
        LENGTH(locks) == 0
        ? [{{ _key: null}}]
        : (
            LET myLocks = (
                FOR myLock in locks
                FILTER 
                    myLock.user.Username == user.Username &&
                    myLock.lock.SecuritySessionId == ""{5}""
                RETURN myLock.lock
            )
            RETURN ( LENGTH(myLocks) > 0 ? {{ _key: FIRST(myLocks)._key }} : null )
          )
    )
    LET doUpsert = (FOR l in results FILTER l != null RETURN l)
    FOR r in doUpsert
        UPSERT {{ _key: r._key}}
        INSERT {{ _from: user._id, _to: i._id, Created:  DATE_ISO8601(DATE_NOW()), ModelType: ""{2}"", TargetType: ""{1}"", SourceType: ""{3}"", Expires: DATE_ISO8601(DATE_ADD(DATE_NOW(), 5, 'i')), IsDeleted: false, IsExtension: false, SecuritySessionId: ""{5}""}}
        UPDATE {{ Expires: DATE_ISO8601(DATE_ADD(DATE_NOW(), 5, 'i')), Extended:  DATE_ISO8601(DATE_NOW()), IsExtension: true}}
        IN " + ModelCollectionManager.EdgeCollection + @"
        RETURN {{""Lock"": NEW, ""Model"": i}}";

            var items = new StringBuilder();
            items.AppendLine("LET items = [FIRST(");
            items.AppendLine(peek);
            items.AppendLine(")]");
            items.AppendLine("FOR i in items");
            items.AppendLine("FILTER i != null");
            items.AppendLine(string.Format(template,
                SecurityContext.Current.CurrentPrincipal.Identity.Name,
                ModelTypeManager.GetModelName(typeof(T)),
                ModelTypeManager.GetModelName(typeof(ILock)),
                ModelTypeManager.GetModelName(typeof(IUser)),
                ModelTypeManager.GetModelName(typeof(IUser)).Replace(".", "_"),
                SecurityContext.Current.Provider.DeviceId));


            return items.ToString();
        }

        public static string BuildLockQuery<T>(T item) where T : IModel
        {
            var template = @"
    LET locks = (
        FOR lock in " + ModelCollectionManager.EdgeCollection + @"
        FOR user in {5}
        FILTER 
        (
            lock.ModelType == ""{3}"" &&
            lock._to == ""{0}"" &&
            lock._from == user._id &&
            lock.Expires > DATE_ISO8601(DATE_NOW()) &&
            lock.IsDeleted == false
        )
        RETURN {{ ""lock"": lock, ""user"": user}}
    )
    LET user = FIRST(
        FOR u in {5}
        FILTER u.Username == ""{1}""
        RETURN u
    )
    LET results = (
        LENGTH(locks) == 0
        ? [{{ _key: null}}]
        : (
            LET myLocks = (
                FOR myLock in locks
                FILTER 
                    myLock.user.Username == user.Username &&
                    myLock.lock.SecuritySessionId == ""{6}""
                RETURN myLock.lock
            )
            RETURN ( LENGTH(myLocks) > 0 ? {{ _key: FIRST(myLocks)._key }} : null )
          )
    )
    LET item = FIRST(
        FOR i in {7}
        FILTER i._id == ""{0}""
        RETURN i
    )
    LET doUpsert = (FOR l in results FILTER l != null RETURN l)
    FOR r in doUpsert
        UPSERT {{ _key: r._key}}
        INSERT {{ _from: user._id, _to: ""{0}"", Created:  DATE_ISO8601(DATE_NOW()), ModelType: ""{3}"", TargetType: ""{2}"", SourceType: ""{4}"", Expires: DATE_ISO8601(DATE_ADD(DATE_NOW(), 5, 'i')), IsDeleted: false, IsExtension: false, SecuritySessionId: ""{6}""}}
        UPDATE {{ Expires: DATE_ISO8601(DATE_ADD(DATE_NOW(), 5, 'i')), Extended:  DATE_ISO8601(DATE_NOW()), IsExtension: true}}
        IN " + ModelCollectionManager.EdgeCollection + @"
        RETURN {{""Lock"": NEW, ""Model"": item}}";
            return string.Format(template, 
                item.Id(), 
                SecurityContext.Current.CurrentPrincipal.Identity.Name, 
                ModelTypeManager.GetModelName(typeof(T)),
                ModelTypeManager.GetModelName(typeof(ILock)),
                ModelTypeManager.GetModelName(typeof(IUser)),
                ModelTypeManager.GetModelName(typeof(IUser)).Replace(".", "_"),
                SecurityContext.Current.Provider.DeviceId,
                ModelCollectionManager.GetCollectionName(item.ModelType));
        }

        public static string BuildUnlockQuery<T>(T item) where T : IModel
        {
            var template = @"
LET locks = (
    FOR lock in " + ModelCollectionManager.EdgeCollection + @"
    FOR user in {0}
    FILTER
    (
        lock.ModelType == ""{1}"" &&
        lock._to == ""{2}"" &&
        lock._from == user._id &&
        lock.IsDeleted == false &&
        lock.SecuritySessionId == ""{3}"" &&
        user.Username == ""{4}""

    )
    RETURN lock
)
FOR lock in locks
UPDATE {{ _key: lock._key, IsDeleted: true}} in " + ModelCollectionManager.EdgeCollection + @"
RETURN NEW";
            return string.Format(template,
                ModelTypeManager.GetModelName(typeof(IUser)).Replace(".", "_"),
                ModelTypeManager.GetModelName(typeof(ILock)),
                item.Id(),
                SecurityContext.Current.Provider.DeviceId,
                SecurityContext.Current.CurrentPrincipal.Identity.Name);
        }

        public static string BuildPeekQueueQuery<T>(IModelQueue queue, int offset = 0, int limit = -1, bool includeItemsOnHold = false) where T : IModel
        {
            var template = @"{0}
        LET locks = (
            FOR lock in " + ModelCollectionManager.EdgeCollection + @"
            FOR user in {4}
            FILTER 
            (
                lock.ModelType == ""lock"" &&
                lock._to == r._id &&
                lock._from == user._id &&
                lock.Expires > DATE_ISO8601(DATE_NOW()) &&
                (lock.SecuritySessionId != ""{5}"" ||
                user.Username != ""{6}"")
            )
            RETURN lock
        )
        LET overrides = (
            FOR queue in {2}
            FOR edge in " + ModelCollectionManager.EdgeCollection + @"
            FILTER
            edge.IsDeleted == false &&
            queue._key == ""{1}"" &&
            edge._from == queue._id &&
            edge._to == r._id &&
            edge.ModelType == ""queueOverrides""
            RETURN MERGE(edge, {{Rank: {3}}})
        )
        FOR r_override IN (
            LENGTH(overrides) > 0 ? overrides : [{{ ForcedRank: 0, Rank: {3}, IsDeleted: false, IsOnHold: false, ModelType: ""queueOverrides""}}]
        )
        FILTER LENGTH(locks) == 0";
            if (!includeItemsOnHold)
            {
                template += " && r_override.IsOnHold == false";
            }

            template += System.Environment.NewLine;

            var source = new StringBuilder();
            StringBuilder sb = null;
            foreach (var query in queue.Queries)
            {
                var joinQueryPipeline = BuildJoinQueryPipeline<T>(query.Query);
                var bqlExpressions = joinQueryPipeline.OfType<JoinAggregateQueryStep<T>>()
                                                            .Select(step => BqlTreeConverter.Convert(step.Predicate))
                                                            .ToArray();
                sb = new StringBuilder();
                var varIdx = 0;
                var returns = joinQueryPipeline.OfType<JoinReturnQueryStep<T>>().FirstOrDefault();

                foreach (var exp in bqlExpressions)
                {
                    sb.AppendLine(string.Format("LET q{0} = ({1})", varIdx++, BuildJoinStatement<T>(exp, returns)));
                }

                if (bqlExpressions.Count() > 1)
                {
                    var statement = "";
                    BuildJoinAggregateStatement<T>(1, joinQueryPipeline.OfType<JoinAggregateQueryStep<T>>().ToArray(), ref statement);
                    sb.AppendLine("FOR r in " + statement);
                }
                else
                {
                    sb.AppendLine("FOR r in q0");
                }

                
                var overrides = string.Format(template, 
                    sb.ToString(), 
                    queue.Key,
                    ModelTypeManager.GetModelName(queue.ModelType).Replace(".", "_"), 
                    query.Rank,
                    ModelTypeManager.GetModelName(typeof(IUser)).Replace(".", "_"),
                    SecurityContext.Current.Provider.DeviceId,
                    SecurityContext.Current.CurrentPrincipal.Identity.Name);
                sb = new StringBuilder(overrides);

                var sortExpression = joinQueryPipeline.OfType<JoinSortQueryStep<T>>().FirstOrDefault();
                if (sortExpression != null)
                {
                    sb.AppendLine(BuildJoinSortStatement(sortExpression));
                }

                var limitExpression = joinQueryPipeline.OfType<JoinLimitQueryStep<T>>().FirstOrDefault();
                if (limitExpression != null)
                {
                    sb.Append(BuildJoinLimitStatement(limitExpression));
                }

                //sb.AppendLine("RETURN r");
                sb.AppendLine(@"RETURN MERGE(r, { ""_queue"": r_override})");

                if (source.Length > 0)
                {
                    var newSource = new StringBuilder();
                    newSource.AppendLine("UNION_DISTINCT((" + sb.ToString() + "), (" + source.ToString() + "))");
                    source = newSource;
                }
                else
                {
                    source = sb;
                }
            }


            var result = new StringBuilder("FOR item in (");
            result.AppendLine(source.ToString() + ")");
            result.AppendLine("SORT item._queue.ForcedRank DESC, item._queue.Rank DESC");
            if (limit > 0)
            {
                result.AppendLine(string.Format("LIMIT {0}, {1}", offset, limit));
            }
            result.AppendLine("RETURN item");

            return result.ToString();
        }

        private static string BuildJoinLimitStatement<T>(JoinLimitQueryStep<T> limitExpression) where T : IModel
        {
            return "LIMIT " + limitExpression.Offset + ", " + limitExpression.Limit + System.Environment.NewLine;
        }

        private static void BuildJoinAggregateStatement<T>(int index, JoinAggregateQueryStep<T>[] traversalExpressions, ref string statement)
        {
            if (index >= traversalExpressions.Length) return;
            if (index == 1)
            {
                if (traversalExpressions[index] is JoinUnionQueryStep<T>)
                {
                    statement = "UNION_DISTINCT(q0, q1)";
                }
                else if (traversalExpressions[index] is JoinIntersectQueryStep<T>)
                {
                    statement = "INTERSECTION(q0, q1)";
                }
                else if (traversalExpressions[index] is JoinExcludeQueryStep<T>)
                {
                    statement = "MINUS(q0, q1)";
                }
            }
            else
            {
                if (traversalExpressions[index] is JoinUnionQueryStep<T>)
                {
                    statement = string.Format("UNION_DISTINCT({0}, q{1})", statement, index);
                }
                else if (traversalExpressions[index] is JoinIntersectQueryStep<T>)
                {
                    statement = string.Format("INTERSECTION({0}, q{1})", statement, index);
                }
                else if (traversalExpressions[index] is JoinExcludeQueryStep<T>)
                {
                    statement = string.Format("MINUS({0}, q{1})", statement, index);
                }
            }
            BuildJoinAggregateStatement<T>(++index, traversalExpressions, ref statement);
        }

        private static string BuildJoinSortStatement<T>(JoinSortQueryStep<T> sortExpression) where T : IModel
        {
            /*
            sort
	        :	'SORT'
	        ;

            sortOrder
	        :	'ASC'
	        |	'DESC'
	        ;

            sortExpression
	        :	sort namedElement sortOrder? (',' namedElement sortOrder?)*
	        ;
            */
            var sb = new StringBuilder();

            for (int i = 0; i < sortExpression.SortExpression.ChildCount; i++)
            {
                Visit(sortExpression.SortExpression.GetChild(i), sb);
            }
            return sb.ToString() + System.Environment.NewLine;
        }


        private static void Visit(IParseTree parseTree, StringBuilder sb)
        {
            if (parseTree is BQLParser.SortContext)
            {
                sb.Append("SORT ");
            }
            else if (parseTree is BQLParser.NamedElementContext)
            {
                sb.Append("r.");
                for (int i = 0; i < parseTree.ChildCount; i++)
                {
                    Visit(parseTree.GetChild(i), sb);
                }
            }
            else if (parseTree is BQLParser.SortOrderContext)
            {
                sb.Append(" " + parseTree.GetText());
            }
            else
            {
                sb.Append(parseTree.GetText());
            }
        }

        private static string BuildJoinStatement<T>(BQLExpression exp, JoinReturnQueryStep<T> returns) where T : IModel
        {
            //string[] vertices, edges;
            //var selectorStatement = BuildJoinSelectors<T>(exp, out vertices, out edges);
            //var filterStatement = BuildJoinFilterStatement<T>(exp, vertices, edges);
            //return selectorStatement + filterStatement + BuildJoinReturnFilterStatement<T>(exp, returns, vertices, edges);
            string query = "";
            int subQueryDepth = 0;
            int depth = 0;
            BuildJoinStatement<T>(exp.PathFilter, null, returns, ref depth, ref query, ref subQueryDepth);
            query += System.Environment.NewLine;
            if (returns.ReturnType == ReturnType.Paths)
            {
                query += string.Format("FOR r in qv{0}[{1}] RETURN r", depth - 1, new string[subQueryDepth].Select(s => "*").Aggregate((s1, s2) => s1 + s2));
            }
            else
            {
                query += string.Format(@"FOR r in qv{0}[{1}] RETURN UNSET(r, ""_edges"")", depth - 1, new string[subQueryDepth].Select(s => "*").Aggregate((s1, s2) => s1 + s2));
            }
            return query;
        }

        private static void BuildJoinStatement<T>(EdgeNodeFilterExpression pathFilter, EdgeNodeFilterExpression child, JoinReturnQueryStep<T> returns, ref int depth, ref string query, ref int subQueryDepth) where T : IModel
        {
            if (pathFilter == null) return;

            var subQuery = BuildJoinElementStatement<T>(pathFilter, child, depth, false, query, ref subQueryDepth, returns);
            if (pathFilter.EdgeType != null)
            {
                subQuery = BuildJoinElementStatement<T>(pathFilter, child, depth, true, subQuery, ref subQueryDepth, returns);
            }
            query = subQuery;
            depth++;
            BuildJoinStatement<T>(pathFilter.Parent as EdgeNodeFilterExpression, pathFilter, returns, ref depth, ref query, ref subQueryDepth);
        }

        private static string BuildJoinElementStatement<T>(EdgeNodeFilterExpression pathFilter, EdgeNodeFilterExpression child, int depth, bool isEdge, string subQuery, ref int subQueryDepth, JoinReturnQueryStep<T> returns) where T : IModel
        {
            /*
              
            Identification.User{Username = 'Foo' | Username = 'Fum'}~>isMemberOf~>Identification.UserGroup

            depth0: isMemberOf(e0) -> IUserGroup(v0), Exclusive
            depth1: null -> IUser(v1), Inclusive
             
            LET q0 = 
            (
                LET qv1 = 
                (
                    FOR v1 in Identification_User
                    LET qe0 = 
                    (
                        FOR e0 in Edge
                        LET qv0 = 
                        (
                            FOR v0 in Identification_UserGroup
                            FILTER 
                            v0.IsDeleted == false && 
                            v0._id == e0._to &&
                            (v0.Name == "Foo")
                            RETURN v0
                        )
            
                        FILTER e0.IsDeleted == false && 
                        e0._from == v1._id &&
                        (e0._key == "1234")
                        RETURN (FOR i in qv0[*] RETURN {"e0": e0, "v0": i.v0})
                    )
                    FILTER 
                    v1.IsDeleted == false && 
                    LENGTH(qe0) == 0 &&
                    (v1.Username == "Foo" || v1.Username == "Fum")
                    RETURN (FOR i in qe0[**] RETURN {"v1": v1, "e0": i.e0, "v0": i.v0})
                )
                FOR r in qv1[***] RETURN r
            )
            FOR r in q0
            RETURN r

            */

            Type edgeType = null;
            Type modelType = null;

            if (!string.IsNullOrEmpty(pathFilter.ModelType))
            {
                modelType = ModelTypeManager.GetModelType(pathFilter.ModelType);
                //ModelSecurityManager.Demand(modelType, DataActions.Read);
            }
            if (!string.IsNullOrEmpty(pathFilter.EdgeType))
            {
                edgeType = ModelTypeManager.GetModelType(pathFilter.EdgeType);
                //ModelSecurityManager.Demand(ModelTypeManager.GetModelType(pathFilter.EdgeType), DataActions.Read);
            }

            var query = new StringBuilder();
            
            // build node query segment first
            var v1 = isEdge ? ModelCollectionManager.GetCollectionName(edgeType) : ModelCollectionManager.GetCollectionName(modelType);
            var v5 = isEdge ? "e" : "v";
            var v2 = string.Format("{0}{1}.IsDeleted == false", v5, depth);

            if (modelType?.Implements<ILink>() ?? false)
            {
                v2 += string.Format(" && {0}{1}.ModelType == '{2}'", v5, depth, ModelTypeManager.GetModelName(modelType));
            }

            query.AppendLine(string.Format("LET q{0}{1} = (", v5, depth));
            query.AppendLine(string.Format("FOR {0}{1} in {2}", v5, depth, v1));
            // get the org unit owner
            //LET owner = 
            //(
            //    FOR v in 1 INBOUND n Edge
            //    FILTER v.ModelType == 'OrgUnit'
            //    RETURN v
            //)
            //RETURN MERGE(n, {_owner: FIRST(owner)})
            if (pathFilter.Parent == null || returns.ReturnType == ReturnType.Paths)
            {
                query.AppendLine(string.Format("LET {0}{1}O = (", v5, depth));
                query.AppendLine(string.Format("FOR v in 1 INBOUND {0}{1} {2}", v5, depth, ModelCollectionManager.EdgeCollection));
                query.AppendLine(string.Format("FILTER v.ModelType == '{0}'", ModelCollectionManager.GetCollectionName<IOrgUnit>()));
                query.AppendLine("RETURN v");
                query.AppendLine(")");
            }
            if (!string.IsNullOrEmpty(subQuery))
            {
                query.AppendLine(subQuery);
            }

            if (isEdge)
            {
                // this node connects to a sub-node thru the edge of the child
                v2 += string.Format(" && e{0}._{1} == v{2}._id", depth, pathFilter is InEdgeNodeFilterExpression ? "to" : "from", depth + 1);
            }
            else if (pathFilter.Parent != null)
            {
                v2 += string.Format(" && e{0}._{1} == v{0}._id", depth, pathFilter is InEdgeNodeFilterExpression ? "from" : "to");
            }
            var filter = "";
            if (isEdge)
            {
                filter = BuildJoinEdgeFilterStatement<T>(pathFilter, depth);
                if (pathFilter.EdgeType != ModelTypeManager.GetModelName(typeof(any)))
                {
                    v2 += string.Format(@" && e{0}.ModelType == ""{1}""", depth, pathFilter.EdgeType);
                }
            }
            else
            {
                filter = BuildJoinModelFilterStatement<T>(pathFilter, depth);
            }

            if (depth > 0)
            {
                if (child.EdgeSelectionType == EdgeSelectionType.Inclusive)
                {
                    v2 += string.Format(" && LENGTH(q{0}{1}) > 0", isEdge ? "v" : "e", isEdge ? depth : depth - 1);
                }
                else if (!isEdge && child.EdgeSelectionType != EdgeSelectionType.OptionalInclusive)
                {
                    v2 += string.Format(" && LENGTH(qe{0}) == 0", isEdge ? depth : depth - 1);
                }
            }
            else if (isEdge && pathFilter.EdgeSelectionType == EdgeSelectionType.Inclusive)
            {
                v2 += string.Format(" && LENGTH(qv{0}) > 0", depth);
            }

            if (!string.IsNullOrEmpty(filter))
            {
                v2 += " && " + filter;
            }

            query.AppendLine(string.Format("FILTER {0}", v2));
            query.Append("RETURN ");

            //RETURN MERGE(n, {_owner: FIRST(owner)})
            var ownedItem = "";
            if (pathFilter.Parent == null || returns.ReturnType == ReturnType.Paths)
            {
                ownedItem = string.Format("MERGE({0}{1}, {{_owner: FIRST({0}{1}O)}})", isEdge ? "e" : "v", depth);
            }
            else
            {
                ownedItem = string.Format("{0}{1}", isEdge ? "e" : "v", depth);
            }

            if (subQueryDepth > 0)
            {
                if (isEdge)
                {
                    query.AppendFormat(@"MERGE({0}, {{""_{1}"": FIRST(q{2}{3}[{4}])}}) ", ownedItem, isEdge ? "model" : "edges", isEdge ? "v" : "e", isEdge ? depth : depth - 1, new string[subQueryDepth].Select(s => "*").Aggregate((s1, s2) => s1 + s2));
                }
                else
                {
                    query.AppendFormat(@"MERGE({0}, {{""_{1}"": q{2}{3}[{4}]}}) ", ownedItem, isEdge ? "model" : "edges", isEdge ? "v" : "e", isEdge ? depth : depth - 1, new string[subQueryDepth].Select(s => "*").Aggregate((s1, s2) => s1 + s2));
                }
            }
            else
            {
                query.Append(ownedItem);
            }

            query.AppendLine();
            query.AppendLine(")");

            subQueryDepth++;
            return query.ToString();
        }

        private static string BuildJoinModelFilterStatement<T>(EdgeNodeFilterExpression pathFilter, int depth) where T : IModel
        {
            return BuildJoinFilterStatement<T>(pathFilter, false, depth);
        }

        private static string BuildJoinEdgeFilterStatement<T>(EdgeNodeFilterExpression pathFilter, int depth) where T : IModel
        {
            return BuildJoinFilterStatement<T>(pathFilter, true, depth);
        }

        private static string BuildJoinFilterStatement<T>(EdgeNodeFilterExpression exp, bool edgeFilter, int depth) where T : IModel
        {
            return JoinEdgeNodeFilterExpressionStatementBuilder.Create<T>(exp, edgeFilter, depth);
        }

        private static string BuildQueryStatement<T>(long offset, long pageSize, PredicateExpression filterExpression, params SortExpression[] sortExpressions) where T : IModel
        {
            //ModelSecurityManager.Demand<T>(DataActions.Read);
            var sb = new StringBuilder();
            sb.AppendLine(string.Format("FOR {0} in {1} ", GetItemName<T>(), ModelCollectionManager.GetCollectionName<T>()));
            sb.AppendLine(BuildPredicateStatement<T>(filterExpression));
            if (sortExpressions != null)
            {
                sb.AppendLine(BuildSortStatement<T>(sortExpressions));
            }
            sb.AppendLine(string.Format("LIMIT {0}, {1}", offset, pageSize));
            sb.AppendLine(string.Format("RETURN {0}", GetItemName<T>()));
            return sb.ToString();
        }

        internal static string BuildCreateQuery<T>(T item) where T : IModel
        {
            var modelKey = ((ModelBase)(object)item).Key;
            string json = null;

            if (item is ILink)
            {
                var sb = new StringBuilder();
                sb.Append("{");
                bool isFirst = true;
                foreach (var prop in ((ILink)item).ModelType.GetPublicProperties().Where(p => (p.CanRead && p.CanWrite) || p.Name.Equals("ModelType")))
                {
                    if (!isFirst)
                    {
                        sb.Append(", ");
                    }

                    if (prop.Name.Equals("To"))
                    {
                        var modelType = ((ILink)item).To.ModelType;
                        var key = string.Format("\"{0}/{1}\"", ModelCollectionManager.GetCollectionName(modelType), ((ILink)item).To.GetKey());
                        sb.Append(string.Format("\"{0}\": {1}, \"TargetType\": \"{2}\"", "_to", key, ModelTypeManager.GetModelName(modelType)));
                    }
                    else if (prop.Name.Equals("From"))
                    {
                        var modelType = ((ILink)item).From.ModelType;
                        var key = string.Format("\"{0}/{1}\"", ModelCollectionManager.GetCollectionName(modelType), ((ILink)item).From.GetKey());
                        sb.Append(string.Format("\"{0}\": {1}, \"SourceType\": \"{2}\"", "_from", key, ModelTypeManager.GetModelName(modelType)));
                    }
                    //else if (prop.Name.Equals("Key")) continue;
                    else if (prop.Name.Equals("ModelType"))
                    {
                        sb.Append(string.Format("\"ModelType\": \"{0}\"", ModelTypeManager.GetModelName((Type)prop.GetValue(item))));
                    }
                    else
                    {
                        sb.Append(string.Format("\"{0}\":{1}", prop.Name, JsonValue(prop.GetValue(item), prop.PropertyType)));
                    }

                    isFirst = false;
                }
                sb.Append("}");
                json = sb.ToString();
            }
            else
            {
                json = item.ToJson();
            }

            if (modelKey > 0)
            {
                json = json.Replace(string.Format("\"Key\":{0}", modelKey), string.Format("\"_key\": \"{0}\"", modelKey));
            }
            else
            {
                json = json.Replace(string.Format("\"Key\":{0},", modelKey), "");
                json = json.Replace(string.Format(", \"Key\":{0}", modelKey), ""); // in case Key is the last property in the json
            }

            var query = string.Format("INSERT {0} IN {1} RETURN NEW", json, ModelCollectionManager.GetCollectionName<T>());
            return query;
        }

        internal static string BuildUpdateQuery<T>(T item) where T : IModel
        {
            var modelKey = ((ModelBase)(object)item).Key;
            string json = null;
            if (item is ILink)
            {
                var sb = new StringBuilder();
                sb.Append("{");
                bool isFirst = true;
                foreach (var prop in ((ILink)item).ModelType.GetPublicProperties().Where(p => (p.CanRead && p.CanWrite) || p.Name.Equals("ModelType")))
                {
                    if (!isFirst && !prop.Name.Equals("Key"))
                    {
                        sb.Append(", ");
                    }

                    if (prop.Name.Equals("To"))
                    {
                        var modelType = ((ILink)item).To.ModelType;
                        var key = string.Format("\"{0}/{1}\"", ModelCollectionManager.GetCollectionName(modelType), ((ILink)item).To.GetKey());
                        sb.Append(string.Format("\"{0}\": {1}, \"TargetType\": \"{2}\"", "_to", key, modelType));
                    }
                    else if (prop.Name.Equals("From"))
                    {
                        var modelType = ((ILink)item).From.ModelType;
                        var key = string.Format("\"{0}/{1}\"", ModelCollectionManager.GetCollectionName(modelType), ((ILink)item).From.GetKey());
                        sb.Append(string.Format("\"{0}\": {1}, \"SourceType\": \"{2}\"", "_from", key, modelType));
                    }
                    else if (prop.Name.Equals("Key")) continue;
                    else if (prop.Name.Equals("ModelType"))
                    {
                        sb.Append(string.Format("\"ModelType\": \"{0}\"", ModelTypeManager.GetModelName((Type)prop.GetValue(item))));
                    }
                    else
                    {
                        sb.Append(string.Format("\"{0}\": {1}", prop.Name, JsonValue(prop.GetValue(item), prop.PropertyType)));
                    }

                    isFirst = false;
                }
                sb.Append("}");
                json = sb.ToString();
            }
            else
            {
                json = item.ToJson().Replace(string.Format("\"Key\":{0},", modelKey), "");

            }
            string modelIdentifier;
            var lockCheck = BuildLockCheckPrefix<T>(item, out modelIdentifier);
            var ownerPrefix = string.Format(System.Environment.NewLine + @"LET owner = (FOR v in 1 INBOUND ""{0}"" {1} FILTER v.ModelType == ""{2}"" RETURN v)" + System.Environment.NewLine,
                ModelCollectionManager.GetIdFromGlobalKey(item.GlobalKey()),
                ModelCollectionManager.EdgeCollection,
                ModelTypeManager.GetModelName<IOrgUnit>());
            var query = string.Format("{3}{4}UPSERT {{_key: {0}}} "
                                         + "INSERT {1} "
                                         + "UPDATE {1} "
                                         + "IN {2} "
                                         + "RETURN MERGE(NEW, {{_owner: FIRST(owner)}})", modelKey == 0 ? "null" : modelIdentifier + "._key", json, ModelCollectionManager.GetCollectionName<T>(), lockCheck, ownerPrefix);
            return query;
        }

        private static string BuildLockCheckPrefix<T>(T item, out string itemIdentifier) where T : IModel
        {
            var template = @"
LET locks = (
    FOR lock in " + ModelCollectionManager.EdgeCollection + @"
    FOR user in {0}
    FILTER
    ( 
        lock.ModelType == ""{1}"" &&
        lock._to == ""{2}"" &&
        lock._from == user._id &&
        lock.IsDeleted == false &&
        lock.SecuritySessionId == ""{3}"" &&
        user.Username == ""{4}""

    )
    RETURN lock
)
FOR lock in locks
FOR model in {5}
FILTER model._id == lock._to
";
            itemIdentifier = "model";
            return string.Format(template,
                ModelTypeManager.GetModelName(typeof(IUser)).Replace(".", "_"),
                ModelTypeManager.GetModelName(typeof(ILock)),
                item.Id(),
                SecurityContext.Current.Provider.DeviceId,
                SecurityContext.Current.CurrentPrincipal.Identity.Name,
                ModelCollectionManager.GetCollectionName(item.ModelType));
        }

        private static string JsonValue(object value, Type propertyType)
        {
            if (value == null) return "null";
            if (propertyType.Equals(typeof(DateTime)) || propertyType.Equals(typeof(string)) || propertyType.IsEnum)
            {
                return string.Format("\"{0}\"", value.ToString());
            }
            if (propertyType.IsValueType)
            {
                return value.ToString();
            }
            return value.ToJson();
        }

        internal static string BuildGetQuery<T>(T item) where T : IModel
        {
            return BuildGetQuery(typeof(T), item.GetKey());
        }

        internal static string BuildGetQuery(Type modelType, string key)
        {
            // ModelSecurityManager.Demand(modelType, DataActions.Read);
            var template = @"
FOR n in {0} 
FILTER n._key ==  ""{1}""
LET owner =
(
    FOR v in 1 INBOUND n {2}
    FILTER v.ModelType == '{3}'
    RETURN v
)
RETURN MERGE(n, {{ _owner: FIRST(owner)}})";
            var query = string.Format(template,
                ModelCollectionManager.GetCollectionName(modelType),
                key,
                ModelCollectionManager.EdgeCollection,
                ModelCollectionManager.GetCollectionName<IOrgUnit>());
            return query;
        }

        internal static string BuildDeleteQuery<T>(T item) where T : IModel
        {
            //ModelSecurityManager.Demand<T>(DataActions.Delete);
            var modelKey = ((ModelBase)(object)item).Key;
            //var query = string.Format("FOR patient in Patient "
            //            +"FILTER patient._key == \"{0}\" "
            //            +"REMOVE patient IN Patient "
            //            +"LET deleted = OLD "
            //            +"RETURN deleted", modelKey, GetCollectionName<T>());
            //return query;
            string modelIdentifier;
            var lockCheck = BuildLockCheckPrefix<T>(item, out modelIdentifier);
            var query = string.Format("{2}UPDATE {{_key: {0}}} WITH {{ IsDeleted: true}} in {1} RETURN NEW", modelIdentifier + "._key", ModelCollectionManager.GetCollectionName<T>(), lockCheck);
            return query;
        }


        public static string GetItemName<T>()
        {
            var type = typeof(T);
            if (type.Name.StartsWith("I"))
            {
                return type.Name.Substring(1, type.Name.Length - 1).ToLower();
            }
            else
            {
                return type.Name.ToLower();
            }
        }

        private static string BuildSortStatement<T>(SortExpression[] sortExpressions)
        {
            if (sortExpressions == null || sortExpressions.Length == 0)
                return null;

            var sort = "";
            foreach(var sortExpression in sortExpressions)
            {
                if (sort.Length > 0)
                {
                    sort += ", ";
                }

                if (sortExpression.SortDirection == SortDirection.Asc)
                {
                    sort += string.Format("{1}.{0}", sortExpression.MemberName, GetItemName<T>());
                }
                else
                {
                    sort += string.Format("{1}.{0} DESC", sortExpression.MemberName, GetItemName<T>());
                }
            }

            return "SORT " + sort;
        }

        private static string BuildPredicateStatement<T>(PredicateExpression filterExpression) where T : IModel
        {
            return PredicateExpressionStatementBuilder.Create<T>(filterExpression);
        }

        public static string BuildAuditInsertStatement(IIdentity user, IEnumerable<IModel> models, AuditEventType eventType, string additionalData)
        {
            if (user == null)
            {
                user = new SuffuzIdentity(IUserDefaults.UnauthorizedUser, "", false);
            }

            if (additionalData?.StartsWith("\"") ?? false)
            {
                additionalData = additionalData.Substring(1, additionalData.Length - 2);
            }

            var auditEntryTemplate = @"{{
  _from: from,
  _to: '{6}',
  'Created': '{0}',
  'IsDeleted': false,
  'ModelType': '{1}',
  'SourceType': '{2}',
  'TargetType': '{3}',
  'AuditEventType': '{4}',
  'AdditionalData': '{5}',
  'ScopeId': '{7}'
}}";
            var insertTemplate = @"LET from = FIRST(FOR u in {1} FILTER u.Username == '{2}' RETURN u)._id
LET events = [{0}]
FOR i in events
INSERT i in {3}
RETURN NEW";

            var sb = new StringBuilder();
            foreach(var model in models)
            {
                if (sb.Length > 0)
                {
                    sb.Append(", ");
                }

                sb.AppendFormat(auditEntryTemplate,
                    DateTime.UtcNow.ToISO8601(),
                    ModelTypeManager.GetModelName(typeof(IAuditEvent)),
                    ModelTypeManager.GetModelName(typeof(IUser)),
                    ModelTypeManager.GetModelName(model is IPath ? ((IPath)model).Root.ModelType : model.ModelType),
                    eventType.ToString(),
                    string.IsNullOrEmpty(additionalData) ? "null" :  additionalData,
                    model.Id(),
                    SecurityContext.Current?.ScopeId ?? Guid.NewGuid().ToString());

                if (model is IPath)
                {
                    foreach(var edge in ((IPath)model).Edges)
                    {
                        sb.Append(", ");
                        sb.AppendFormat(auditEntryTemplate,
                            DateTime.UtcNow.ToISO8601(),
                            ModelTypeManager.GetModelName(typeof(IAuditEvent)),
                            ModelTypeManager.GetModelName(typeof(IUser)),
                            ModelTypeManager.GetModelName(edge.ModelType),
                            eventType.ToString(),
                            string.IsNullOrEmpty(additionalData) ? "null" : "'" + additionalData + "'",
                            edge.Id(),
                            SecurityContext.Current?.ScopeId ?? Guid.NewGuid().ToString());
                    }

                    foreach (var node in ((IPath)model).Nodes.Skip(1))
                    {
                        if (node == null)
                            continue;
                        sb.Append(", ");
                        sb.AppendFormat(auditEntryTemplate,
                            DateTime.UtcNow.ToISO8601(),
                            ModelTypeManager.GetModelName(typeof(IAuditEvent)),
                            ModelTypeManager.GetModelName(typeof(IUser)),
                            ModelTypeManager.GetModelName(node.ModelType),
                            eventType.ToString(),
                            string.IsNullOrEmpty(additionalData) ? "null" : "'" + additionalData + "'",
                            node.Id(),
                            SecurityContext.Current?.ScopeId ?? Guid.NewGuid().ToString());
                    }
                }
            }

            return string.Format(insertTemplate, 
                sb.ToString(), 
                ModelCollectionManager.GetCollectionName<IUser>(), 
                user.Name,
                ModelCollectionManager.AuditCollection);
        }
    }
}

using Antlr4.Runtime.Tree;
using Arango.Client;
using NNHIS.Data;
using Shs.Common;
using Shs.Common.Diagnostics;
using Shs.Data.Core;
using Shs.Data.Core.Compilation;
using Shs.Data.Core.Grammar;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Collections;
using System.Threading;

namespace Shs.Data.ArangoDB
{
    public interface IAqlSource
    {
        string Aql { get; }
        string AqlVariableName { get; }
    }

    public abstract class TraverseQueryStep<T> : IEnumerable<T>, IAqlSource where T : IModel
    {
        public void Execute(ADatabase database, TraverseQueryStep<T> previousStep)
        {
            this.Database = database;
            this.Aql = this.CreateAql(previousStep);
        }

        public string Aql { get; protected set; }
        public ADatabase Database { get; private set; }

        public abstract string AqlVariableName { get; }

        protected abstract string CreateAql(TraverseQueryStep<T> previousStep);

        public IEnumerator<T> GetEnumerator()
        {
#if(DEBUG)
            Logger.LogInfo(Aql);
#endif
            var result = Database.Query.Aql(Aql).ToDocuments();
            var list = new ModelListBuilder<T>()
                .Create(result.Value, 0, result.Value.Count, result.Value.Count);
            if (typeof(T).Implements<ILink>())
            {
                for (int i = 0; i < list.Count; i++)
                {
                    var link = list[i] as LinkBase;
                    var _to = link._to.Replace("_", ".").Split('/');
                    var _from = link._from.Replace("_", ".").Split('/');
                    var toType = ModelTypeManager.GetModelType(_to[0]);
                    var toQuery = QueryBuilder.BuildGetQuery(toType, _to[1]);
                    var fromType = ModelTypeManager.GetModelType(_from[0]);
                    var fromQuery = QueryBuilder.BuildGetQuery(fromType, _from[1]);
                    var to = Database.Query.Aql(toQuery).ToDocument().Value;
                    link.To = (IModel)RuntimeModelBuilder.CreateModelInstanceActivator(toType)(to);
                    var from = Database.Query.Aql(fromQuery).ToDocument().Value;
                    link.From = (IModel)RuntimeModelBuilder.CreateModelInstanceActivator(fromType)(from);
                }
            }
            return list.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }

    public class TraverseReturnForQueryStep<T> : TraverseQueryStep<T> where T : IModel
    {
        public override string AqlVariableName
        {
            get { return "r"; }
        }

        protected override string CreateAql(TraverseQueryStep<T> previousStep)
        {
            return previousStep.Aql
                + System.Environment.NewLine
                + "FOR " + AqlVariableName + " IN " + previousStep.AqlVariableName;
        }
    }

    public class TraverseSortQueryStep<T> : TraverseQueryStep<T> where T : IModel
    {
        public BQLParser.SortExpressionContext SortExpression { get; set; }

        string _var;
        public override string AqlVariableName
        {
            get { return _var; }
        }

        private string  BuildSortExpression(TraverseQueryStep<T> previousStep)
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

            for(int i = 0; i < SortExpression.ChildCount; i++)
            {
                this.Visit(SortExpression.GetChild(i), sb, previousStep);
            }
            return sb.ToString();
        }

        private void Visit(IParseTree parseTree, StringBuilder sb, TraverseQueryStep<T> previousStep)
        {
            if (parseTree is BQLParser.SortContext)
            {
                sb.Append("SORT ");
            }
            else if (parseTree is BQLParser.NamedElementContext)
            {
                sb.Append(previousStep.AqlVariableName + ".");
                for (int i = 0; i < parseTree.ChildCount; i++)
                {
                    Visit(parseTree.GetChild(i), sb, previousStep);
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

        protected override string CreateAql(TraverseQueryStep<T> previousStep)
        {
            _var = previousStep.AqlVariableName;
            return previousStep.Aql
                + System.Environment.NewLine
                + BuildSortExpression(previousStep);
        }
    }

    public class TraverseLimitQueryStep<T> : TraverseQueryStep<T> where T : IModel
    {
        public TraverseLimitQueryStep()
        {
        }
        public int Limit { get; private set; }
        public int Offset { get; private set; }

        public BQLParser.LimitExpressionContext LimitExpression { get; set; }

        string _var;
        public override string AqlVariableName
        {
            get { return _var; }
        }

        protected override string CreateAql(TraverseQueryStep<T> previousStep)
        {
            GetValues();
            _var = previousStep.AqlVariableName;
            return previousStep.Aql + System.Environment.NewLine
                   + "LIMIT " + Offset + ", " + Limit; 
        }

        private void GetValues()
        {
            if (LimitExpression.ChildCount == 3)
            {
                // we have LIMIT Offset, Count
                this.Offset = int.Parse(LimitExpression.GetChild(1).GetText());
                this.Limit = int.Parse(LimitExpression.GetChild(2).GetText());
            }
            else
            {
                // we have LIMIT Count
                this.Offset = 0;
                this.Limit = int.Parse(LimitExpression.GetChild(1).GetText());
            }
        }
    }

    public class TraverseReturnQueryStep<T> : TraverseQueryStep<T> where T : IModel
    {
        string _var;
        public override string AqlVariableName
        {
            get { return _var; }
        }



        protected override string CreateAql(TraverseQueryStep<T> previousStep)
        {
            _var = previousStep.AqlVariableName;
            return previousStep.Aql 
                + System.Environment.NewLine 
                + "RETURN " + AqlVariableName;
        }
    }

    public abstract class TraverseAggregateQueryStep<T> : TraverseQueryStep<T>, IEqualityComparer<T> where T : IModel
    {
        static long _variable = 0;
        public BQLParser.PredicateExpressionContext Predicate { get; set; }
        protected TraverseAggregateQueryStep()
        {
            _variableName = GetVariableName();
        }

        protected string _variableName;
        public override string AqlVariableName
        {
            get
            {
                return _variableName;
            }
        }

        public bool Equals(T x, T y)
        {
            if (x == null && y == null) return true;
            if (x == null || y == null) return false;
            return x.GetKey().Equals(y.GetKey());
        }

        public int GetHashCode(T obj)
        {
            return 0; // forces the comparison to evaluate equality
        }

        protected string GetVariableName()
        {
            long var = Interlocked.Increment(ref _variable);
            return "v" + var;
        }
    }

    public class TraverseUnionQueryStep<T> : TraverseAggregateQueryStep<T> where T : IModel
    {
        protected override string CreateAql(TraverseQueryStep<T> previousStep)
        {
            var expression = BqlTreeConverter.Convert(this.Predicate);
            var query = QueryBuilder.BuildTraversalQuery<IApp>(expression);
            if (previousStep == null)
            {
                query = "LET " + AqlVariableName + " = (" + query + ")";
            }
            else
            { 
                query = previousStep.Aql + System.Environment.NewLine 
                        + "LET " + AqlVariableName + " = (" + query + ")";
                var letVar = GetVariableName();
                query += System.Environment.NewLine
                        + "LET " + letVar + " = UNION_DISTINCT(" + previousStep.AqlVariableName + ", " + AqlVariableName + ")";
                _variableName = letVar;
            }

            return query;
        }
    }

    public class TraverseIntersectQueryStep<T> : TraverseAggregateQueryStep<T> where T : IModel
    {
        protected override string CreateAql(TraverseQueryStep<T> previousStep)
        {
            var expression = BqlTreeConverter.Convert(this.Predicate);
            var query = QueryBuilder.BuildTraversalQuery<IApp>(expression);
            if (previousStep != null)
            {
                query = previousStep.Aql + System.Environment.NewLine
                        + "LET " + AqlVariableName + " = (" + query + ")";
                var letVar = GetVariableName();
                query += System.Environment.NewLine
                        + "LET " + letVar + " = INTERSECTION(" + previousStep.AqlVariableName + ", " + AqlVariableName + ")";
                _variableName = letVar;
            }

            return query;
        }
    }

    public class TraverseExcludeQueryStep<T> : TraverseAggregateQueryStep<T> where T : IModel
    {
        protected override string CreateAql(TraverseQueryStep<T> previousStep)
        {
            var expression = BqlTreeConverter.Convert(this.Predicate);
            var query = QueryBuilder.BuildTraversalQuery<IApp>(expression);
            if (previousStep != null)
            {
                query = previousStep.Aql + System.Environment.NewLine
                        + "LET " + AqlVariableName + " = (" + query + ")";
                var letVar = GetVariableName();
                query += System.Environment.NewLine
                        + "LET " + letVar + " = MINUS(" + previousStep.AqlVariableName + ", " + AqlVariableName + ")";
                _variableName = letVar;
            }

            return query;
        }
    }
}

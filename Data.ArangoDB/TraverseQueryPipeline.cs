using Antlr4.Runtime;
using Antlr4.Runtime.Tree;
using Arango.Client;
using Shs.Data.Core;
using Shs.Data.Core.Grammar;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Shs.Data.ArangoDB
{
    public class TraverseQueryPipeline<T> : IEnumerable<TraverseQueryStep<T>> where T : IModel
    {
        List<TraverseQueryStep<T>> _steps = new List<TraverseQueryStep<T>>();
        public TraverseQueryPipeline(BQLParser.QueryExpressionContext bqlExpression)
        {
            this.Visit(bqlExpression);
            if (step != null)
            {
                _steps.Add(step);
                step = null;
            }

            var s = _steps.Last();
            if (!(s is TraverseSortQueryStep<T>) && !(s is TraverseLimitQueryStep<T>))
            {
                _steps.Add(new TraverseReturnForQueryStep<T>());
            }
            _steps.Add(new TraverseReturnQueryStep<T>());
        }

        public ModelList<T> Execute(ADatabase database)
        {
            TraverseQueryStep<T> lastStep = null;
            foreach (var step in _steps)
            {
                step.Execute(database, lastStep);
                lastStep = step;
            }
            var items = _steps.Last().ToArray();
            var offset = 0;
            var limit = _steps.SingleOrDefault(s => s is TraverseLimitQueryStep<T>);
            if (limit != null)
            {
                offset = ((TraverseLimitQueryStep<T>)limit).Offset;
            }
            return new ModelList<T>(items, offset, items.Length, items.Length, items.Length);
        }

        public IEnumerator<TraverseQueryStep<T>> GetEnumerator()
        {
            return _steps.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        TraverseAggregateQueryStep<T> step = null;
        private void Visit(IParseTree context)
        {
            if (context is BQLParser.QueryExpressionContext)
            {
                // queryExpression
	            //        : aggregateExpression sortExpression? rankExpression ?
                //        ;
                for (int i = 0; i < context.ChildCount; i++)
                {
                    this.Visit(context.GetChild(i));
                }
            }
            else if(context is BQLParser.AggregateExpressionContext)
            {
                /* 
                
                aggregateExpression
	                :	'(' aggregateExpression ')'
	                |	predicateExpression (aggregator predicateExpression)*
	                ;
                
                */

                if (step == null)
                {
                    step = new TraverseUnionQueryStep<T>();
                }
                else
                { 
                    _steps.Add(step);
                }


                for (int i = 0; i < context.ChildCount; i++)
                {
                    this.Visit(context.GetChild(i));
                }
            }
            else if (context is BQLParser.AggregatorContext)
            {
                /* 
                
                aggregator
	                :	'UNION'
	                |	'INTERSECT'
	                |	'EXCLUDE'
	                ;
            
                */
                _steps.Add(step);
                if (context.GetText() == "UNION")
                {
                    step = new TraverseUnionQueryStep<T>();
                }
                else if (context.GetText() == "INTERSECT")
                {
                    step = new TraverseIntersectQueryStep<T>();
                }
                else if (context.GetText() == "EXCLUDE")
                {
                    step = new TraverseExcludeQueryStep<T>();
                }
            }
            else if (context is BQLParser.PredicateExpressionContext)
            {
                /* 
                
                predicateExpression
	                :	vertexAccessor ((edgeAccessor | vertexIn | vertexOut) vertexAccessor)*
	                ;

                */
                step.Predicate = context as BQLParser.PredicateExpressionContext;
            }
            else if (context is BQLParser.SortExpressionContext)
            {
                _steps.Add(step);
                _steps.Add(new TraverseReturnForQueryStep<T>());
                _steps.Add(new TraverseSortQueryStep<T>() { SortExpression = context as BQLParser.SortExpressionContext });
                step = null;
            }
            else if (context is BQLParser.LimitExpressionContext)
            {
                /*
                limit
	            :	'LIMIT'
	            ;

                limitExpression
	            :	limit (integerLiteral,)? integerLiteral
                */
                if (step != null)
                    _steps.Add(step);
                if (!(_steps.Last() is TraverseSortQueryStep<T>))
                {
                    _steps.Add(new TraverseReturnForQueryStep<T>());
                }
                _steps.Add(new TraverseLimitQueryStep<T>() { LimitExpression = context as BQLParser.LimitExpressionContext });
                step = null;
            }

        }

    }
}

using Antlr4.Runtime.Tree;
using Common;
using Common.Diagnostics;
using Common.Security;
using Data.Core;
using Data.Core.Compilation;
using Data.Core.Grammar;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Data.Core.Grammar
{
    public class JoinQueryPipeline<T> : IEnumerable<JoinQueryStep<T>> where T : IModel
    {
        List<JoinQueryStep<T>> _steps = new List<JoinQueryStep<T>>();
        public JoinQueryPipeline(BQLParser.QueryExpressionContext bqlExpression)
        {
            this.Visit(bqlExpression);
            if (step != null)
            {
                _steps.Add(step);
                step = null;
            }

            var s = _steps.Last();

            if (_steps.OfType<JoinReturnForQueryStep<T>>().Count() == 0)
            {
                _steps.Add(new JoinReturnForQueryStep<T>());
                _steps.Add(new JoinReturnQueryStep<T>(ReturnType.Nodes));
            }
            else if (_steps.OfType<JoinReturnQueryStep<T>>().Count() == 0)
            {
                _steps.Add(new JoinReturnQueryStep<T>(ReturnType.Nodes));
            }
        }

        protected List<JoinQueryStep<T>> Steps { get { return _steps; } }


        public IEnumerator<JoinQueryStep<T>> GetEnumerator()
        {
            return _steps.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }



        JoinAggregateQueryStep<T> step = null;
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
            else if (context is BQLParser.AggregateExpressionContext)
            {
                /* 
                
                aggregateExpression
	                :	'(' aggregateExpression ')'
	                |	predicateExpression (aggregator predicateExpression)*
	                ;
                
                */

                if (step == null)
                {
                    step = new JoinUnionQueryStep<T>();
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
                    step = new JoinUnionQueryStep<T>();
                }
                else if (context.GetText() == "INTERSECT")
                {
                    step = new JoinIntersectQueryStep<T>();
                }
                else if (context.GetText() == "EXCLUDE")
                {
                    step = new JoinExcludeQueryStep<T>();
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
                _steps.Add(new JoinReturnForQueryStep<T>());
                _steps.Add(new JoinSortQueryStep<T>() { SortExpression = context as BQLParser.SortExpressionContext });
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
                if (!(_steps.Last() is JoinSortQueryStep<T>))
                {
                    _steps.Add(new JoinReturnForQueryStep<T>());
                }
                _steps.Add(new JoinLimitQueryStep<T>() { LimitExpression = context as BQLParser.LimitExpressionContext });
                step = null;
            }
            else if (context is BQLParser.ProjectorExpressionContext)
            {
                /*
                projectorExpression
	                :	projector projectorType
	                ;

                projector
	                :	'RETURNS'
	                ;

                projectorType
	                :	'NODES'
	                |	'PATHS'
	                ;
                */
                if (step != null)
                    _steps.Add(step);
                if (!(_steps.Last() is JoinSortQueryStep<T>))
                {
                    _steps.Add(new JoinReturnForQueryStep<T>());
                }
                _steps.Add(new JoinReturnQueryStep<T>(((BQLParser.ProjectorTypeContext)context.GetChild(1)).GetText() == "PATHS" ? ReturnType.Paths : ReturnType.Nodes));
                step = null;
            }

        }
    }
}

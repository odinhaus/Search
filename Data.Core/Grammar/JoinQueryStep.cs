using Data.Core;
using Data.Core.Grammar;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Data.Core.Grammar
{
    public enum ReturnType
    {
        Nodes,
        Paths
    }
    public abstract class JoinQueryStep<T>
    {
        
    }

    public class JoinReturnForQueryStep<T> : JoinQueryStep<T>
    {
    }

    public class JoinSortQueryStep<T> : JoinQueryStep<T> 
    {
        public BQLParser.SortExpressionContext SortExpression { get; set; }
    }

    public class JoinLimitQueryStep<T> : JoinQueryStep<T>
    {
        public JoinLimitQueryStep()
        {
        }
        public int Limit { get; private set; }
        public int Offset { get; private set; }

        private BQLParser.LimitExpressionContext _exp;
        public BQLParser.LimitExpressionContext LimitExpression
        {
            get { return _exp; }
            set
            {
                _exp = value;
                if (_exp != null)
                {
                    GetValues();
                }
            }
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

    public class JoinReturnQueryStep<T> : JoinQueryStep<T>
    {
        public JoinReturnQueryStep(ReturnType returnType)
        {
            this.ReturnType = returnType;
        }

        public ReturnType ReturnType { get; private set; }
    }

    public abstract class JoinAggregateQueryStep<T> : JoinQueryStep<T>
    {
        public BQLParser.PredicateExpressionContext Predicate { get; set; }
    }

    public class JoinUnionQueryStep<T> : JoinAggregateQueryStep<T> 
    {
        
    }

    public class JoinIntersectQueryStep<T> : JoinAggregateQueryStep<T>
    {
       
    }

    public class JoinExcludeQueryStep<T> : JoinAggregateQueryStep<T>
    {
        
    }
}

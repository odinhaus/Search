using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;

namespace Data.Core.Linq
{
    public class InEdgeNodeFilterExpression : EdgeNodeFilterExpression
    {
        public InEdgeNodeFilterExpression() : base() { }
        public InEdgeNodeFilterExpression(EdgeSelectionType selectionType, Type edgeType, Type nodeType, Expression predicate, Expression parent)
            : base(selectionType, edgeType, nodeType, predicate, parent)
        {
        }

        [JsonIgnore]
        public override ExpressionType NodeType
        {
            get
            {
                return (ExpressionType)QueryExpressionType.InEdgeNodeFilter;
            }
        }
    }
}

﻿using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;

namespace Data.Core.Linq
{
    public class OutEdgeNodeFilterExpression : EdgeNodeFilterExpression
    {
        public OutEdgeNodeFilterExpression() { }
        public OutEdgeNodeFilterExpression(EdgeSelectionType selectionType, Type edgeType, Type nodeType, Expression predicate, Expression parent)
            : base(selectionType, edgeType, nodeType, predicate, parent)
        {
        }

        [JsonIgnore]
        public override ExpressionType NodeType
        {
            get
            {
                return (ExpressionType)QueryExpressionType.OutEdgeNodeFilter;
            }
        }
    }
}

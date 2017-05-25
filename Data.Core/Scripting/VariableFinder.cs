using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;

namespace Data.Core.Scripting
{
    public class VariableFinder : Data.Core.Linq.ExpressionVisitor
    {
        protected List<ParameterExpression> Parameters { get; private set; }

        public static ParameterExpression[] FindParameters(IEnumerable<Expression> statements)
        {
            var finder = new VariableFinder();
            foreach (var statement in statements)
            {
                finder.Visit(statement);
            }
            return finder.Parameters.Distinct().ToArray();
        }

        private VariableFinder()
        {
            this.Parameters = new List<ParameterExpression>();
        }

        HashSet<ParameterExpression> _seen = new HashSet<ParameterExpression>();
        protected override Expression VisitBinary(BinaryExpression b)
        {
            if (b.Left is ParameterExpression
                && ((BinaryExpression)b).NodeType == ExpressionType.Assign
                && !_seen.Contains(b.Left as ParameterExpression))
            {
                var p = VariableScope.Search(((ParameterExpression)b.Left).Name) as ParameterExpression;
                if (p != null)
                {
                //    throw new InvalidOperationException(string.Format("The variable '{0}' referenced is out of scope for the assignment.", ((ParameterExpression)b.Left).Name));
                //}
                //else
                //{
                    this.Parameters.Add(p);
                }
            }
            return base.VisitBinary(b);
        }

        protected override Expression VisitUnknown(Expression expression)
        {
            return expression;
        }

        protected override Expression VisitBlock(BlockExpression node)
        {
            //return node;
            var parms = new List<Expression>();
            var exps = new List<Expression>();
            foreach (var parm in node.Variables)
            {
                parms.Add(Visit(parm));
            }
            foreach (var statement in node.Expressions)
            {
                exps.Add(Visit(statement));
            }
            return UpdateBlock(node, parms.Cast<ParameterExpression>().ToArray(), exps);
        }

        protected virtual Expression UpdateBlock(BlockExpression node, ParameterExpression[] parameterExpression, List<Expression> exps)
        {
            if (!node.Variables.All(v => parameterExpression.Any(p => p == v)) || !node.Expressions.All(e => exps.Any(x => x == e)))
            {
                return Expression.Block(parameterExpression, exps);
            }
            else
            {
                return node;
            }
        }
    }
}

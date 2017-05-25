using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;

namespace Data.Core.Scripting
{
    public class VariableSubstitutor : Data.Core.Linq.ExpressionVisitor
    {
        

        public static Expression Replace(Expression source, Expression variable, Expression replacement)
        {
            var visitor = new VariableSubstitutor(variable, replacement);
            return visitor.Visit(source);
        }

        private VariableSubstitutor(Expression variable, Expression replacement)
        {
            this.Variable = variable;
            this.Replacement = replacement;
        }

        public Expression Variable { get; private set; }
        public Expression Replacement { get; private set; }

        protected override Expression VisitParameter(ParameterExpression p)
        {
            if (p == null || !p.Equals(this.Variable))
                return base.VisitParameter(p);
            else
                return this.Replacement;
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;

namespace Data.Core.Linq
{
    public class FieldAssignment
    {
        FieldExpression field;
        Expression expression;

        public FieldAssignment(FieldExpression field, Expression expression)
        {
            this.field = field;
            this.expression = expression;
        }

        public FieldExpression Field
        {
            get { return this.field; }
        }

        public Expression Expression
        {
            get { return this.expression; }
        }
    }
}

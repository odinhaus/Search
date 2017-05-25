using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;

namespace Data.Core.Linq
{
    public class NamedValueExpression : Expression
    {
        string name;
        StorageType queryType;
        Expression value;

        public NamedValueExpression() { }
        public NamedValueExpression(string name, StorageType queryType, Expression value)
        {
            if (name == null)
                throw new ArgumentNullException("name");
            //if (queryType == null)
            //throw new ArgumentNullException("queryType");
            if (value == null)
                throw new ArgumentNullException("value");
            this.name = name;
            this.queryType = queryType;
            this.value = value;
        }

        public string Name
        {
            get { return this.name; }
        }

        public StorageType QueryType
        {
            get { return this.queryType; }
        }

        public Expression Value
        {
            get { return this.value; }
        }

        public override ExpressionType NodeType
        {
            get
            {
                return (ExpressionType)QueryExpressionType.NamedValue;
            }
        }

        public override Type Type
        {
            get
            {
                return Value.Type;
            }
        }

        public override string ToString()
        {
            return QueryExpressionWriter.WriteToString(this);
        }
    }
}

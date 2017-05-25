using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;

namespace Data.Core.Linq
{
    public class DateFunctionExpression : FunctionExpression
    {
        public DateFunctionExpression() : base() { }
        public DateFunctionExpression(DateFunctionType functionType, Type returnType, params BinarySerializableExpression[] args)
            : base ((QueryExpressionType)functionType, returnType, args)
        {
        }
    }
}

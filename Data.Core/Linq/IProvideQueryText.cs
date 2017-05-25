using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;

namespace Data.Core.Linq
{
    public interface IProvideQueryText
    {
        string ToString(Expression queryExpression);
    }
}

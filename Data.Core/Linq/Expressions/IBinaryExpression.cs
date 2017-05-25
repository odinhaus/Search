using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Data.Core.Linq
{
    public interface IBinaryExpression
    {
        string MemberName { get; }
        BinarySerializableExpression Value { get; }
    }
}

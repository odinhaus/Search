using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Data.Core
{
    public interface IModelList : IEnumerable
    {
        long Offset { get; }
        long TotalRecords { get; }
        int PageSize { get; }
        int PageCount { get; }
    }
}

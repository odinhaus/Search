using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Data.Core
{
    public interface IProxyModel
    {
        bool IsValid { get; }
        Type ModelType { get; }
        IRepository Repository { get; }
    }
}

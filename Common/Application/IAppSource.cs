using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Common.Application
{
    public interface IAppSource
    {
        IEnumerable<DeclaredApp> Apps { get; }
        DeclaredApp this[string appName] { get; }
    }
}

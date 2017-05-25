using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Common.DI
{
    public interface IContainerMappings : IEnumerable<IContainerMapping>
    {
        IContainerMapping Add();
    }
}

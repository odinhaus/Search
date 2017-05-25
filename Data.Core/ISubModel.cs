using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Data.Core
{
    // marker interface used to identify complex sub-model types that are embedded in model types
    public interface ISubModel : IModel
    {
    }
}

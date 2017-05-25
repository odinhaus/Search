using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Data.Core.Security
{
    public interface IOrgUnitInitializer
    {
        IOrgUnit Create(string name, string prefix);
    }
}

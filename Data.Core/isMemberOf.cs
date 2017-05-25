using Common.Security;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Data.Core
{
    [Model("isMemberOf", "Shs")]
    public interface isMemberOf : ILink<long>
    {
    }
}

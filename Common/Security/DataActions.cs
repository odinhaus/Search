using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Common.Security
{
    [Flags]
    public enum DataActions : ulong
    {
        Custom = 0,
        Read = 1,
        Create = 2,
        Update = 4,
        Delete = 8,
        Link = 16,
        All = Read | Create | Update | Delete | Link,
        Write = Create | Update | Link
    }
}

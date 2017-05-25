using StructureMap;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Common.DI
{
    public abstract class Registry
    {
        public abstract IContainer Initialize();
    }
}

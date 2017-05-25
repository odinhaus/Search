using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Data.Core.Linq
{
    public abstract class QueryTypeSystem
    {
        public abstract StorageType GetStorageType(Type type);
    }
}

using Data.Core.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Data.Core.Linq.Win
{
    public class ClientTypeSystem : QueryTypeSystem
    {
        public override StorageType GetStorageType(Type type)
        {
            return new ClientStorageType(type);
        }
    }
}

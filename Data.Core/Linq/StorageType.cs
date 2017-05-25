using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Data.Core.Linq
{
    public abstract class StorageType
    {
        public abstract int ToInt32();
        public abstract Type ToNativeType();

        public abstract string TypeName { get; }
    }
}

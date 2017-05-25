using Data.Core.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Data.Core.Linq.Win
{
    public class ClientStorageType : StorageType
    {
        private Type type;

        public ClientStorageType(Type type)
        {
            this.type = type;
        }

        public override string TypeName
        {
            get
            {
                return type.FullName;
            }
        }

        public override int ToInt32()
        {
            return type.GetHashCode();
        }

        public override Type ToNativeType()
        {
            return type;
        }
    }
}

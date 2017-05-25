using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Data.Core.Linq
{
    public class ModelAlias
    {
        public ModelAlias()
        {
        }

        public override string ToString()
        {
            return "A:" + this.GetHashCode();
        }
    }
}

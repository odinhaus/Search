using Altus.Suffūz.Serialization.Binary;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Data.Core.Security
{
    public interface IOwnedModel
    {
        IOrgUnit Owner { get; }
    }

    public interface _IOwnedModel : IOwnedModel
    {
        [BinarySerializable(10000)]
        new IOrgUnit Owner { get; set; }
    }

    public class Test : _IOwnedModel
    {
        public IOrgUnit Owner
        {
            get;
            set;
        }

        IOrgUnit IOwnedModel.Owner
        {
            get
            {
                return this.Owner;
            }
        }
    }
}

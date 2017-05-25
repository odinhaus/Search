using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Common.Collections
{
    public interface INotifyCollectionChanging
    {
        event CollectionChangingHandler CollectionChanging;
    }
}

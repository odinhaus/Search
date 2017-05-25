using Data.Core.Domains.Operations;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Data.Core
{
    public interface IModelQueueProvider<T> where T : IModel
    {
        ModelList<QueuedModel<T>> Peek(IModelQueue queue, int offset = 0, int count = -1, bool includeItemsOnHold = false);
        QueuedModel<T> Dequeue(IModelQueue queue);
        void Hold(IModelQueue queue, T item);
        void Release(IModelQueue queue, T item);
        int QueuedCount(IModelQueue queue);
    }
}

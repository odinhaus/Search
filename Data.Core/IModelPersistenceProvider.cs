using Data.Core.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Data.Core
{
    public interface IModelPersistenceProvider<T> where T :IModel
    {
        T Save(SaveExpression expression);
        int Delete(DeleteExpression expression);
        int Delete(T item);
        T Create(T item, IOrgUnit owner);
        T Update(T item);
        T Get(T item);
        LockedModel<T> Lock(T item);
        ILock Unlock(T item);
    }
}

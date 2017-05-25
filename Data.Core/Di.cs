using Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;

namespace Data.Core
{
    public static class Di
    {
        public static T Get<T>()
        {
            return AppContext.Current.Container.GetInstance<T>();
        }

        public static IModelQueryProvider<T> QueryProvider<T>() where T : IModel
        {
            return AppContext.Current.Container.GetInstance<IModelQueryProviderBuilder>().CreateQueryProvider<T>();
        }

        public static ModelList<IAny> Query<T>(string query) where T : IModel
        {
            return QueryProvider<T>().Query(query);
        }

        public static IModelPersistenceProvider<T> PersistenceProvider<T>() where T : IModel
        {
            return AppContext.Current.Container.GetInstance<IModelPersistenceProviderBuilder>().CreatePersistenceProvider<T>();
        }

        public static T GetModel<T>(T model) where T : IModel
        {
            return PersistenceProvider<T>().Get(model);
        }

        public static T CreateModel<T>(T model, IOrgUnit owner) where T : IModel
        {
            if (typeof(T) == typeof(IModel))
            {
                var mi = typeof(Di).GetMethod("PersistenceProvider", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static).MakeGenericMethod(model.ModelType);
                var ppType = typeof(IModelPersistenceProvider<>).MakeGenericType(model.ModelType);
                var mipp = ppType.GetMethod("Create");
                var pModel = Expression.Parameter(typeof(T));
                var pOwner = Expression.Parameter(typeof(IOrgUnit));
                var callPP = Expression.Call(null, mi);
                var callCreate = Expression.Convert(Expression.Call(callPP, mipp, Expression.Convert(pModel, model.ModelType), pOwner), typeof(T));

                var lambda = Expression.Lambda<Func<T, IOrgUnit, T>>(callCreate, pModel, pOwner).Compile();
                return lambda(model, owner);
            }
            else
            {
                return PersistenceProvider<T>().Create(model, owner);
            }
        }

        public static T UpdateModel<T>(T model) where T : IModel
        {
            return PersistenceProvider<T>().Update(model);
        }

        public static int DeleteModel<T>(T model) where T : IModel
        {
            return PersistenceProvider<T>().Delete(model);
        }

        public static LockedModel<T> LockModel<T>(T model) where T : IModel
        {
            return PersistenceProvider<T>().Lock(model);
        }

        public static ILock UnlockModel<T>(T model) where T : IModel
        {
            return PersistenceProvider<T>().Unlock(model);
        }
    }
}

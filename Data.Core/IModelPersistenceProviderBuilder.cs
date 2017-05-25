using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Data.Core
{
    public interface IModelPersistenceProviderBuilder
    {
        /// <summary>
        /// Creates a default IModelPersistenceProvider for model of modelType
        /// </summary>
        /// <param name="modelType"></param>
        /// <returns></returns>
        object CreatePersistenceProvider(Type modelType);
        /// <summary>
        /// Creates a default IModelPersistenceProvider for model of type T
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        IModelPersistenceProvider<T> CreatePersistenceProvider<T>() where T : IModel;
    }
}

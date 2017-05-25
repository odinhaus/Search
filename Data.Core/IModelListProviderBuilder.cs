using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Data.Core
{
    public interface IModelListProviderBuilder
    {
        /// <summary>
        /// Creates a default IModelListProvider for model of modelType
        /// </summary>
        /// <param name="modelType"></param>
        /// <returns></returns>
        object CreateListProvider(Type modelType);
        /// <summary>
        /// Creates a default IModelListProvider for model of type T
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        IModelListProvider<T> CreateListProvider<T>();
    }
}

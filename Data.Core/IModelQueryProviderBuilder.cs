using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Data.Core
{
    public interface IModelQueryProviderBuilder
    {
        /// <summary>
        /// Creates a default IModelQueryProvider for model of modelType
        /// </summary>
        /// <param name="modelType"></param>
        /// <returns></returns>
        object CreateQueryProvider(Type modelType);
        /// <summary>
        /// Creates a default IModelQueryProvider for model of type T
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        IModelQueryProvider<T> CreateQueryProvider<T>() where T : IModel;
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Data.Core
{
    public interface IModelQueueProviderBuilder
    {
        /// <summary>
        /// Creates a default IModelQueryProvider for model of modelType
        /// </summary>
        /// <param name="modelType"></param>
        /// <returns></returns>
        object CreateQueueProvider(Type modelType);
        /// <summary>
        /// Creates a default IModelQueryProvider for model of type T
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        IModelQueueProvider<T> CreateQueueProvider<T>() where T : IModel;
    }
}

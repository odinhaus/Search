using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Data.Core
{
    public interface IModelSet<T> : IModelSet, IDataSet<T>
        where T : IModel
    {
        /// <summary>
        /// Gets a populated Thing instance for the provided activity
        /// </summary>
        /// <param name="model"></param>
        /// <returns></returns>
        T Get(T model);
    }

    public interface IModelSet : IDataSet
    {
        /// <summary>
        /// Gets a populated IIdentifiable instance with the specified IIdentity
        /// </summary>
        /// <param name="model"></param>
        /// <returns></returns>
        IModel Get(IModel model);
    }
}

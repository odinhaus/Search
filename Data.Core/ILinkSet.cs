using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Data.Core
{
    public interface ILinkSet<T> : ILinkSet, IModelSet<T>
        where T : ILink
    {
        /// <summary>
        /// Gets a populated Thing instance for the provided activity
        /// </summary>
        /// <param name="model"></param>
        /// <returns></returns>
        new T Get(T model);
    }

    public interface ILinkSet : IModelSet
    {
        /// <summary>
        /// Gets a populated IIdentifiable instance with the specified IIdentity
        /// </summary>
        /// <param name="model"></param>
        /// <returns></returns>
        ILink Get(ILink model);
    }
}

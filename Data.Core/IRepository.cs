using Data.Core.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Data.Core
{
    public interface IRepository : IDisposable, IExecuteQueries
    {
        /// <summary>
        /// Gets a boolean value indicating whether the IRepository instance is valid for use.
        /// </summary>
        bool IsValid { get; }
        /// <summary>
        /// Creates an queryable instance of Thing types specified by T
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        IModelSet<T> ModelSet<T>() where T : IModel;
        /// <summary>
        /// Creates an queryable instance of Association types specified by T
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        ILinkSet<T> LinkSet<T>() where T : ILink;
        /// <summary>
        /// Gets a populated Thing instance for the provided activity
        /// </summary>
        /// <param name="model"></param>
        /// <returns></returns>
        T Get<T>(T model) where T : IModel;
        /// <summary>
        /// Gets a populated IIdentifiable instance with the specified IIdentity
        /// </summary>
        /// <param name="model"></param>
        /// <returns></returns>
        IModel Get(IModel model);
        /// <summary>
        /// Gets a populated IIdentifiable instance by the specified Id
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        IModel Get(string id);
        /// <summary>
        /// Gets a typed identifiable by its Id value
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="id"></param>
        /// <returns></returns>
        T Get<T>(string id) where T : IModel;
        /// <summary>
        /// Gets the policy to use for queries on this repository instance
        /// </summary>
        IQueryPolicy Policy { get; }
        /// <summary>
        /// Gets an instance of the query cache
        /// </summary>
        ICacheQueries QueryCache { get; }
        /// <summary>
        /// Creates an instance of the persistence provider which is compatible with this repository
        /// </summary>
        /// <returns></returns>
        IPersistableProvider CreateProvider();
    }
}

using Data.Core.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Data.Core
{
    public interface IExecuteQueries
    {
        /// <summary>
        /// Execute a raw query statement directly against the store, without returning any results
        /// </summary>
        /// <param name="query">a structured query in the laguage of the underlying provider</param>
        int Delete<T>(string query) where T : IModel;
        /// <summary>
        /// Execute a raw query statement firectly against the store, and marshal the results into an 
        /// enumerable of type T
        /// </summary>
        /// <typeparam name="T">the identifiable type to marshal results into</typeparam>
        /// <param name="query">a structured query in the laguage of the underlying provider</param>
        /// <returns></returns>
        T Save<T>(string query) where T : IModel;
        /// <summary>
        /// Executes a BQL query against the data store, and returns an enumerable list of T
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="query"></param>
        /// <returns></returns>
        ModelList<T> Query<T>(string query) where T : IModel;
    }
}

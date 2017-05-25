using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Data.Core
{
    public interface IDataSet<T> : IDataSet, IPersistable<T> where T : IModel
    { }

    public interface IDataSet : IPersistable
    {
        /// <summary>
        /// Gets the IRepository instance associated with this IThingSet
        /// </summary>
        IRepository Repository { get; }
    }
}

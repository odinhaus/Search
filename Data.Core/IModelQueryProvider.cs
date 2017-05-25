using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Data.Core
{
    public interface IModelQueryProvider<T> where T : IModel
    {
        ModelList<IAny> Query(string query);
        object Raw(string query);
    }
}

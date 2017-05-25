using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Data.Core.Templating
{
    public interface IModelTemplateProvider<T> where T : IModel
    {
        string View(IDDVTemplate template, T model);
    }
}

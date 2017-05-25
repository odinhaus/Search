using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Data.Core.Templating
{
    public interface IModelTemplateProviderBuilder
    {
        IModelTemplateProvider<T> CreateTemplateProvider<T>() where T : IModel;
        object CreateTemplateProvider(Type modelType);
    }
}

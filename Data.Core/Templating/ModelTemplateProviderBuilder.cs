using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Data.Core.Templating
{
    public class ModelTemplateProviderBuilder : IModelTemplateProviderBuilder
    {
        public object CreateTemplateProvider(Type modelType)
        {
            var type = typeof(ModelTemplateProvider<>).MakeGenericType(modelType);
            return Activator.CreateInstance(type);
        }

        public IModelTemplateProvider<T> CreateTemplateProvider<T>() where T : IModel
        {
            return CreateTemplateProvider(typeof(T)) as IModelTemplateProvider<T>;
        }
    }
}

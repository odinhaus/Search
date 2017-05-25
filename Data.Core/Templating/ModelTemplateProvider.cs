using Common;
using Common.Security;
using Data.Core.Evaluation;
using Data.Core.Security;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Data.Core.Templating
{
    public class ModelTemplateProvider<T> : IModelTemplateProvider<T> where T : IModel
    {
        public string View(IDDVTemplate template, T model)
        {
            var mpp = AppContext.Current.Container.GetInstance<IModelPersistenceProviderBuilder>().CreatePersistenceProvider<T>();
            model = mpp.Get(model);
            template = GetTemplate(template);

            if (!template.ModelTypes.Any(t => t.Equals(ModelTypeManager.GetModelName<T>())))
                throw new InvalidOperationException("The template does not support the provided model type.");

            var func = DDVTemplateEvaluator.Evaluate<T>(template.Template, template.GlobalKey());
            var runtime = AppContext.Current.Container.GetInstance<IRuntimeBuilder>().Create(
                Common.Security.DataActions.Read,
                null,
                SecurityContext.Current.ToUser(),
                model,
                typeof(T),
                new Auditing.AuditedChange[0]
                );

            var html = func(runtime);
            return html;
        }

        private IDDVTemplate GetTemplate(IDDVTemplate template)
        {
            var mpp = AppContext.Current.Container.GetInstance<IModelPersistenceProviderBuilder>().CreatePersistenceProvider<IDDVTemplate>();
            template = mpp.Get(template);

            return template;
        }
    }
}

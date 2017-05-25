using Common.Security;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Data.Core.Security
{
    public class ModelInstanceAuthorizationEvaluator : ICustomAuthorizationEvaluator
    {
        public ModelInstanceAuthorizationEvaluator(Func<IEnumerable<IModel>> modelAccessor, DataActions actions)
        {
            if (modelAccessor == null)
                throw new InvalidOperationException("The model accessor cannot be null.");
            this.ModelAccessor = modelAccessor;
            this.Actions = actions;
        }

        public DataActions Actions { get; private set; }
        public Func<IEnumerable<IModel>> ModelAccessor { get; private set; }

        public bool Demand()
        {
            string message;
            var models = ModelAccessor();
            if (models == null)
                throw new InvalidOperationException("The model cannot be null.");
            foreach (var model in models)
            {
                if (!DataAccessSecurityContext.Current.Demand(SecurityContext.Current.ToUser(), model, model.ModelType, Actions, out message))
                {
                    return false;
                }
            }
            return true;
        }
    }
}

using Common.Security;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Data.Core.Security
{
    public class ModelTypeAuthorizationEvaluator : ICustomAuthorizationEvaluator
    {
        public ModelTypeAuthorizationEvaluator(Type[] modelTypes, DataActions actions)
        {
            this.ModelTypes = modelTypes;
            this.Actions = actions;
        }

        public DataActions Actions { get; private set; }
        public Type[] ModelTypes { get; private set; }

        public bool Demand()
        {
            foreach(var modelType in ModelTypes)
            {
                string message;
                if (!DataAccessSecurityContext.Current.Demand(SecurityContext.Current.ToUser(), null, modelType, Actions, out message ))
                {
                    return false;
                }
            }
            return true;
        }
    }
}


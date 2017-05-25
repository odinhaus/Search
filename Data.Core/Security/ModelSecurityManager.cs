using Microsoft.IdentityModel.Claims;
using Common;
using Common.Security;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Security;
using System.Text;
using System.Threading.Tasks;

namespace Data.Core.Security
{
    public static class ModelSecurityManager
    {
        public static bool TryDemand<T>(DataActions actions) where T : IModel
        {
            return TryDemand<T>(default(T), actions);
        }

        public static bool TryDemand<T>(T model, DataActions actions) where T : IModel
        {
            string message;
            if (!DataAccessSecurityContext.Current.Demand(Common.Security.SecurityContext.Current.ToUser(), model, typeof(T), actions, out message))
            {
                return false;
            }
            return true;
        }

        public static bool TryDemand(Type modelType, DataActions actions)
        {
            return TryDemand(modelType, null, actions);
        }

        public static bool TryDemand(Type modelType, IModel model, DataActions actions)
        {
            if (!modelType.Implements<IModel>())
                throw new InvalidOperationException("ModelType must implement IModel.");

            string message;
            if (!DataAccessSecurityContext.Current.Demand(Common.Security.SecurityContext.Current.ToUser(), model, modelType, actions, out message))
            {
                return false;
            }
            return true;
        }
    }
}

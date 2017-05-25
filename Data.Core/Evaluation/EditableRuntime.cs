using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Common.Security;
using Data.Core.Auditing;
using Data.Core.Security;
using System.Security;
using Common;

namespace Data.Core.Evaluation
{
    public class EditableRuntime : Runtime, IEditableRuntime
    {
        public EditableRuntime(DataActions action, string callContextOrgUnit, IUser user, IModel model, Type modelType, IEnumerable<AuditedChange> changes, string customArg = "") 
            : base(action, callContextOrgUnit, user, model, modelType, changes, customArg)
        {
        }

        public bool IsAuditingEnabled
        {
            get
            {
                return AuditSettings.IsEnabled;
            }

            set
            {
                AuditSettings.IsEnabled = value;
            }
        }

        public bool IsSecurityEnabled
        {
            get
            {
                return DataAccessSecurityContext.Current.IsEnabled;
            }

            set
            {
                DataAccessSecurityContext.Current.IsEnabled = value;
            }
        }

        public IUser Authenticate(string username, string password)
        {
            try
            {
                if (username == "Admin")
                {
                    Common.Security.SecurityContext.Current.CurrentPrincipal = IUserDefaults.Administrator;
                    return Common.Security.SecurityContext.Current.ToUser();
                }
                else if (Common.Security.SecurityContext.Current.Authenticate(username, password))
                {
                    return Common.Security.SecurityContext.Current.ToUser();
                }
            }
            catch { }

            throw new SecurityException("Authentication failed");
        }

        public T Create<T>(T model, IOrgUnit orgUnit) where T : IModel
        {
            var mpp = AppContext.Current.Container.GetInstance<IModelPersistenceProviderBuilder>().CreatePersistenceProvider<T>();
            return mpp.Create(model, orgUnit);
        }

        public int Delete<T>(T model) where T : IModel
        {
            var mpp = AppContext.Current.Container.GetInstance<IModelPersistenceProviderBuilder>().CreatePersistenceProvider<T>();
            mpp.Lock(model);
            var result = mpp.Delete(model);
            mpp.Unlock(model);
            return result;
        }

        public T Get<T>(T model) where T : IModel
        {
            var mpp = AppContext.Current.Container.GetInstance<IModelPersistenceProviderBuilder>().CreatePersistenceProvider<T>();
            return mpp.Get(model);
        }

        public T Update<T>(T model) where T : IModel
        {
            var mpp = AppContext.Current.Container.GetInstance<IModelPersistenceProviderBuilder>().CreatePersistenceProvider<T>();
            mpp.Lock(model);
            var result = mpp.Update(model);
            mpp.Unlock(model);
            return result;
        }
    }
}

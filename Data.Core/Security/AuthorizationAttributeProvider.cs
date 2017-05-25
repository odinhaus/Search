using Common;
using Common.Security;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Data.Core.Security
{
    //public class AuthorizationAttributeProvider : IDataActionAuthorizationProvider
    //{
    //    public virtual ModelList<IDataActionAuthorization> Get(string modelTypeName)
    //    {
    //        return Get(ModelTypeManager.GetModelType(modelTypeName));
    //    }

    //    public virtual ModelList<IDataActionAuthorization> Get(Type modelType)
    //    {
    //        if (!modelType.Implements<IModel>())
    //            throw new InvalidOperationException("Model Type must implement IModel");
    //        var results = new List<IDataActionAuthorization>();

    //        foreach(var attribute in modelType.GetCustomAttributes(true).Where(a => a.GetType().BaseType.Equals(typeof(DataActionAuthorizeAttribute))))
    //        {
    //            var auth = Model.New<IDataActionAuthorization>();
    //            auth.TargetModelType = ModelTypeManager.GetModelName(modelType);
    //            if (attribute is DataActionAuthorizeAnyUserAttribute)
    //            {
    //                auth.DataActionAuthorizationType = DataActionAuthorizationType.AnyUser;
    //                auth.DataActions = ((DataActionAuthorizeAnyUserAttribute)attribute).Actions;
    //            }
    //            else if (attribute is DataActionAuthorizeCustomAttribute)
    //            {
    //                auth.DataActionAuthorizationType = DataActionAuthorizationType.Custom;
    //                auth.CustomAuthorizationEvaluatorType = ((DataActionAuthorizeCustomAttribute)attribute).Evaluator.AssemblyQualifiedName;
    //                auth.DataActions = ((DataActionAuthorizeCustomAttribute)attribute).Actions;
    //            }
    //            else if (attribute is DataActionAuthorizeRoleAttribute)
    //            {
    //                auth.DataActionAuthorizationType = DataActionAuthorizationType.Role;
    //                auth.RoleName = ((DataActionAuthorizeRoleAttribute)attribute).Role;
    //                auth.DataActions = ((DataActionAuthorizeRoleAttribute)attribute).Actions;
    //            }
    //            else if (attribute is DataActionAuthorizeUserAttribute)
    //            {
    //                auth.DataActionAuthorizationType = DataActionAuthorizationType.User;
    //                auth.UserName = ((DataActionAuthorizeUserAttribute)attribute).User;
    //                auth.DataActions = ((DataActionAuthorizeUserAttribute)attribute).Actions;
    //            }
    //            else if (attribute is DataActionAuthorizeUnrestrictedAttribute)
    //            {
    //                auth.DataActionAuthorizationType = DataActionAuthorizationType.Unrestricted;
    //                auth.DataActions = ((DataActionAuthorizeUnrestrictedAttribute)attribute).Actions;
    //            }
    //            results.Add(auth);
    //        }
    //        return new ModelList<IDataActionAuthorization>(results, 0, results.Count, results.Count, results.Count);
    //    }

    //    public virtual ModelList<IDataActionAuthorization> Get<T>() where T : IModel
    //    {
    //        return Get(typeof(T));
    //    }

    //    public ModelList<IDataActionAuthorization> List()
    //    {
    //        var results = new List<IDataActionAuthorization>();
    //        foreach(var modelType in ModelTypeManager.ModelTypes)
    //        {
    //            foreach (var auth in Get(modelType))
    //                results.Add(auth);
    //        }
    //        return new ModelList<IDataActionAuthorization>(results, 0, results.Count, results.Count, results.Count);
    //    }
    //}
}

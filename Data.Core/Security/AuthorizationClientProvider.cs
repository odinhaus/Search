using Common;
using Common.Web;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Data.Core.Security
{
    //public class AuthorizationClientProvider : IDataActionAuthorizationProvider
    //{
    //    public virtual ModelList<IDataActionAuthorization> Get(string modelTypeName)
    //    {
    //        return AppContext.Current.Container.GetInstance<IApiClient>()
    //            .Call<ModelList<IDataActionAuthorization>>(
    //                new string[] { "DataActionAuthorization", "Get" },
    //                new KeyValuePair<string, object>("modelTypeName", modelTypeName)
    //            );
    //    }
    //    public ModelList<IDataActionAuthorization> Get(Type modelType)
    //    {
    //        return Get(ModelTypeManager.GetModelName(modelType));
    //    }

    //    public ModelList<IDataActionAuthorization> Get<T>() where T : IModel
    //    {
    //        return Get(typeof(T));
    //    }

    //    public ModelList<IDataActionAuthorization> List()
    //    {
    //        return AppContext.Current.Container.GetInstance<IApiClient>()
    //            .Call<ModelList<IDataActionAuthorization>>(new string[] { "DataActionAuthorization", "List" });
    //    }
    //}
}

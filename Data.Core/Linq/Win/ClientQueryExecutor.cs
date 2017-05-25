using Altus.Suffūz.Serialization;
using Common;
using Common.Web;
using Data.Core.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Data.Core.Linq.Win
{
    public class ClientQueryExecutor : QueryExecutor
    {
        private ClientQueryProvider _provider;

        public ClientQueryExecutor(ClientQueryProvider provider)
        {
            _provider = provider;
        }

        public override int Delete<T>(string query)
        {
            var api = AppContext.Current.Container.GetInstance<IApiClient>();
            var modelType = typeof(T);
            var serviceName = ((ModelAttribute)modelType.GetCustomAttributes(typeof(ModelAttribute), true)[0]).FullyQualifiedName;
            var exp = new DeleteExpression();
            exp.FromBytes(System.Convert.FromBase64String(query));
            return api.Call<int>(new string[] { serviceName, "Delete" }, new KeyValuePair<string, object>("expression", exp));
        }

        public override T Save<T>(string query)
        {
            var api = AppContext.Current.Container.GetInstance<IApiClient>();
            var modelType = typeof(T);
            var serviceName = ((ModelAttribute)modelType.GetCustomAttributes(typeof(ModelAttribute), true)[0]).FullyQualifiedName;
            var exp = new SaveExpression();
            exp.FromBytes(System.Convert.FromBase64String(query));
            return api.Call<T>(new string[] { serviceName, "Save" }, new KeyValuePair<string, object>("expression", exp));
        }

        public override ModelList<T> Query<T>(string query)
        {
            var api = AppContext.Current.Container.GetInstance<IApiClient>();
            var modelType = typeof(T);

            if (typeof(T).Implements<IPath>())
            {
                modelType = modelType.GetGenericArguments()[0];
            }

            var serviceName = ((ModelAttribute)modelType.GetCustomAttributes(typeof(ModelAttribute), true)[0]).FullyQualifiedName;
            return api.Call<ModelList<T>>(new string[] { serviceName, "Query" }, new KeyValuePair<string, object>("query", query));
        }
    }
}

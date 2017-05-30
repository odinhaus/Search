using Common.Security;
using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Common.Web
{
    public enum PayloadFormat
    {
        Json,
        Binary
    }
    public interface IApiClient : IDynamicMetaObjectProvider
    {
        void ReportError(Exception ex, string platform = "ios");
        SuffuzPrincipal Authenticate(string username, string password);
        bool ChangePassword(string currentPassword, string newPassword, out string message);
        T Call<T>(string[] uriSegments, params KeyValuePair<string, object>[] args);
    }
}

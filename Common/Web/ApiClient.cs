using Newtonsoft.Json.Linq;
using Common.Auditing;
using Common.Diagnostics;
using Common.Security;
using Common.Serialization;
using System;
using System.Collections.Generic;
using System.Dynamic;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace Common.Web
{
    public partial class ApiClient : DynamicObject, IApiClient
    {
        static ApiClient()
        {
            PayloadFormat = PayloadFormat.Binary;
        }

        public ApiClient()
        {

        }

        public static PayloadFormat PayloadFormat { get; set; }

        public void ReportError(Exception ex, string platform = "ios")
        {
            var request = CreateRequest(RollbarEntry.ROLLBAR_URI, "POST");
            var entry = new RollbarEntry(ex, DateTime.Now, platform).ToString();
            var success = false;
            var result = "";
            try
            {
                request.GetRequestStreamAsync().ContinueWith((st) =>
                {
                    using (var stream = st.Result)
                    {
                        using (var bw = new BinaryWriter(stream))
                        {
                            bw.Write(UTF8Encoding.UTF8.GetBytes(entry));
                        }
                    }
                    request.GetResponseAsync().ContinueWith((rt) =>
                    {
                        using (var response = (HttpWebResponse)rt.Result)
                        {
                            success = response.StatusCode == HttpStatusCode.OK;
                            using (var stream = response.GetResponseStream())
                            {
                                using (var sr = new StreamReader(stream))
                                {
                                    result = sr.ReadToEnd();
                                }
                            }
                        }
                    }).Wait(30000);
                }).Wait(30000);
            }
            catch { }
        }

        public SHSPrincipal Authenticate(string username, string password)
        {
            var uri = AppContext.ApiUris.AuthenticateUri;
            var request = CreateRequest(uri, "POST");
            var success = false;
            var closedResult = "";
            using (var sw = new StreamWriter(request.GetRequestStream()))
            {
                sw.Write(string.Format("grant_type=password&username={0}&password={1}&device_id={2}&verbose=true",
                    username,
                    password,
                    SecurityContext.Global.Provider.DeviceId));
            }
            try
            {
                request.GetResponseAsync().ContinueWith((rt) =>
                {
                    using (var response = (HttpWebResponse)rt.Result)
                    {
                        success = response.StatusCode == HttpStatusCode.OK;

                        using (var stream = response.GetResponseStream())
                        {
                            using (var sr = new StreamReader(stream))
                            {
                                closedResult = sr.ReadToEnd();
                            }
                        }
                    }
                }).Wait(30000);
                var jObj = JObject.Parse(closedResult);
                var identity = new SHSIdentity(
                    jObj["userName"].Value<string>(),
                    jObj["customer_id"].Value<string>(),
                    jObj["access_token"] != null)
                {
                    BearerToken = jObj["access_token"].Value<string>(),
                    TokenExpiration = DateTime.Parse(jObj[".expires"].Value<string>()).ToLocalTime()
                };
                var claims = JArray.Parse(jObj["claims"].Value<string>());
                foreach (var claim in claims)
                {
                    var sc = claim.ToObject<SerializableClaim>();
                    identity.Claims.Add(new Microsoft.IdentityModel.Claims.Claim(sc.Type, sc.Value, sc.ValueType, sc.Issuer, sc.OriginalIssuer));
                }
                return new SHSPrincipal(identity);
            }
            catch(Exception ex)
            {
                return new SHSPrincipal(new SHSIdentity(
                    username,
                    "",
                    false)
                {
                    BearerToken = null
                });
            }
        }

        public bool ChangePassword(string password, string newPassword, out string message)
        {
            var uri = AppContext.ApiUris.ChangePasswordUri;
            var request = CreateRequest(uri, "POST");
            request.ContentType = "application/json";
            var success = false;
            message = "";
            string result = null; ;
            using (var sw = new StreamWriter(request.GetRequestStream()))
            {
                sw.Write(new ChangePasswordModel()
                {
                    OldPassword = password,
                    NewPassword = newPassword,
                    ConfirmPassword = newPassword
                }.ToJson());
            }

            request.GetResponseAsync().ContinueWith((rt) =>
            {
                using (var response = (HttpWebResponse)rt.Result)
                {
                    success = response.StatusCode == HttpStatusCode.OK;

                    try
                    {
                        using (var stream = response.GetResponseStream())
                        {
                            using (var sr = new StreamReader(stream))
                            {
                                result = sr.ReadToEnd();
                            }
                        }
                    }
                    catch (WebException wex)
                    {
                        if (wex.Response != null)
                        {
                            using (var resp = wex.Response)
                            {
                                using (var sr = new StreamReader(resp.GetResponseStream()))
                                {
                                    result = sr.ReadToEnd();
                                }
                            }
                        }
                    }
                }
            }).Wait(30000);
            message = result;
            return !string.IsNullOrEmpty(message);
        }



        public T Call<T>(string[] uriSegments, params KeyValuePair<string, object>[] args)
        {
            var request = ApiClient.CreateRequest(GetUri(uriSegments[0], uriSegments[1]), "POST");
            request.ContentType = "application/json; charset=utf-8";
            request.Accept = "application/json; charset=utf-8";
            var postData = new List<KeyValuePair<string, object>>(args);

            if (postData.Count > 0)
            {
                var json = postData.ToJson();
                var jsonBytes = UTF8Encoding.UTF8.GetBytes(json);
                request.ContentLength = jsonBytes.Length;

                using (var bw = new BinaryWriter(request.GetRequestStream(), UTF8Encoding.UTF8))
                {
                    bw.Write(jsonBytes);
                }
            }
            else
            {
                request.ContentLength = 0;
            }

            try
            {
                var response = (HttpWebResponse)request.GetResponse();
                using (var sr = new StreamReader(response.GetResponseStream()))
                {
                    var resultJson = sr.ReadToEnd();

                    return resultJson.FromJson<T>();
                }
            }
            catch
            {
                throw new IOException();
            }
        }

        private string GetUri(string controllerName, string actionName)
        {
            return AppContext.Current.Container.GetInstance<IResolveApiUris>().Resolve(controllerName, actionName);
        }

        public override bool TryInvoke(InvokeBinder binder, object[] args, out object result)
        {
            return base.TryInvoke(binder, args, out result);
        }

        public override bool TryGetMember(GetMemberBinder binder, out object result)
        {
            result = new VirtualMember(binder.Name, this);
            return true;
        }
        
        private static HttpWebRequest CreateRequest(string uri, string method)
        {            
            var request = (HttpWebRequest)HttpWebRequest.Create(uri);
            var authBuilder = AppContext.Current.Container.GetInstance<IHttpHeaderAuthTokenBuilder>();
            request.Method = method;
            request.Accept = "application/json";
            request.Headers["Accept-Encoding"] = "gzip, deflate";
            request.Headers["Accept-Language"] = "en-US,en;q=0.8";
            request.Headers["Cache-Control"] = "no-cache";
            request.Headers["X-Requested-With"] = AppContext.Current.CurrentApp.Name;
            string header;
            var token = authBuilder.GetRequestToken(out header);
            request.Headers[header] = token;

            var auditTokenWriter = AppContext.Current.Container.GetInstance<IHttpAuditScopeTokenWriter>();
            string auditHeader;
            var scopeId = auditTokenWriter.Write(out auditHeader);
            request.Headers[auditHeader] = scopeId;

            return request;
        }

        class VirtualMember : DynamicObject
        {
            public VirtualMember(string memberName, DynamicObject parent)
            {
                this.MemberName = memberName;
                this.Parent = parent;
            }

            public string MemberName { get; private set; }
            public DynamicObject Parent { get; private set; }

            public override bool TryGetMember(GetMemberBinder binder, out object result)
            {
                result = new VirtualMember(binder.Name, this);
                return true;
            }

            public override bool TryInvoke(InvokeBinder binder, object[] args, out object result)
            {
                var request = ApiClient.CreateRequest(GetUri(((VirtualMember)this.Parent).MemberName, this.MemberName), "POST");
                request.ContentType = "application/json; charset=utf-8";
                request.Accept = "application/json; charset=utf-8";
                var postData = new List<KeyValuePair<string, object>>();
                var i = 0;
                if (binder.CallInfo.ArgumentNames.Count > 0)
                {
                    foreach (var argName in binder.CallInfo.ArgumentNames)
                    {
                        postData.Add(new KeyValuePair<string, object>(argName, args[i]));
                        i++;
                    }
                }
                else
                {
                    for(i = 0; i < binder.CallInfo.ArgumentCount; i++)
                    {
                        postData.Add(new KeyValuePair<string, object>("p" + i + "|" + args[i].GetType().AssemblyQualifiedName, args[i]));
                    }
                }
                if (i > 0)
                {
                    var json = postData.ToJson();
                    var jsonBytes = UTF8Encoding.UTF8.GetBytes(json);
                    request.ContentLength = jsonBytes.Length;
                    
                    using (var bw = new BinaryWriter(request.GetRequestStream(), UTF8Encoding.UTF8))
                    {
                        bw.Write(jsonBytes);
                    }
                }
                else
                {
                    request.ContentLength = 0;
                }

                try
                {
                    var response = (HttpWebResponse)request.GetResponse();
                    using (var sr = new StreamReader(response.GetResponseStream()))
                    {
                        var resultJson = sr.ReadToEnd();

                        result = new VirtualResult(resultJson.FromJson(binder.ReturnType));

                        return true;
                    }
                }
                catch (WebException wex)
                {
                    if (wex.Response != null)
                    {
                        using (var resp = wex.Response)
                        {
                            using (var sr = new StreamReader(resp.GetResponseStream()))
                            {
                                result = sr.ReadToEnd();
                                throw new IOException(
                                    string.Format("An unexpected error occurred while executing a request for '{0}'.  The details are as follows:{1}{2}", 
                                        request.RequestUri, 
                                        System.Environment.NewLine, 
                                        result));
                            }
                        }
                    }
                    else
                    {
                        throw new IOException(string.Format("An unexpected error occurred while executing a request for '{0}'", request.RequestUri));
                    }
                }
            }

            private string GetUri(string controllerName, string actionName)
            {
                return AppContext.Current.Container.GetInstance<IResolveApiUris>().Resolve(controllerName, actionName);
            }
        }

        class VirtualResult : DynamicObject
        {
            public VirtualResult(object value)
            {                
                this.Value = value;
            }

            public object Value { get; private set; }

            public override bool TryConvert(ConvertBinder binder, out object result)
            {
                if (Value == null)
                {
                    result = Value;
                }
                else if (Value is JObject)
                {
                    result = ((JObject)Value).ToObject(binder.ReturnType);
                }
                else if (Value is JArray)
                {
                    result = ((JArray)Value).ToObject(binder.ReturnType);
                }
                else if (!TryCast(Value, binder.ReturnType, out result))
                {
                    result = Convert.ChangeType(Value, binder.ReturnType);
                }
                return true;
            }

            public bool TryCast(object value, Type castType, out object castValue)
            {
                var parm = Expression.Parameter(typeof(object));
                var unbox = Expression.Convert(parm, value.GetType());
                var cast = Expression.Convert(unbox, castType);
                var box = Expression.Convert(cast, typeof(object));
                var lambda = Expression.Lambda<Func<object, object>>(box, parm).Compile();
                try
                {
                    castValue = lambda(value);
                    return true;
                }
                catch
                {
                    castValue = null;
                    return false;
                }
            }
        }
    }
}

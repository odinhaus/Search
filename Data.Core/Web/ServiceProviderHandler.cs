using Altus.Suffūz.Serialization;
using Microsoft.IdentityModel.Claims;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;
using Common;
using Common.Diagnostics;
using Common.Security;
using Common.Serialization;
using Common.Serialization.Binary;
using Common.Web.Handlers;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Linq.Expressions;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Net.Http.Headers;
using System.IO;
using System.Net;
using HiQPdf;

namespace Data.Core.Web
{
    public class ServiceProviderHandler : IDelegatingHandler
    {
        static long REQUEST_NUMBER = 0;
        static Dictionary<string, Func<HttpRequestMessage, object, object[], HttpResponseMessage>> _invokers = new Dictionary<string, Func<HttpRequestMessage, object, object[], HttpResponseMessage>>();

        public object ServiceProvider { get; private set; }

        public ServiceProviderHandler(object serviceProvider)
        {
            this.ServiceProvider = serviceProvider;
        }

        public async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
#if TRACE
            var count = Interlocked.Increment(ref REQUEST_NUMBER);
            Debug.WriteLine("{0}\tHandling Request: {1}", request.RequestUri.PathAndQuery, count);
#endif
            var sec = SecurityContext.Current;
            return await sec.ExecuteAsync(() => InvokeHandler(this.ServiceProvider, request, cancellationToken));
        }

        private HttpResponseMessage InvokeHandler(object serviceProvider, HttpRequestMessage request, CancellationToken cancellationToken)
        {
            MethodInfo methodToCall;
            IOverride overide;
            if (request.Headers.Accept.Any(a => a.MediaType == "application/octet-stream"))
            {
                BinaryRequest payload;
                if (MapBinaryServiceCall(serviceProvider, request, out payload, out methodToCall, out overide))
                {
                    var key = "bin_" + GetMethodSignature(request, payload.Select(p => p.Key).ToArray());
                    Func<HttpRequestMessage, object, object[], HttpResponseMessage> invoker;
                    lock (_invokers)
                    {
                        if (!_invokers.TryGetValue(key, out invoker))
                        {
                            invoker = BuildBinaryExecutor(methodToCall, overide);
                            _invokers.Add(key, invoker);
                        }
                    }
                    return AuthorizeAndInvoke(serviceProvider, methodToCall, () => invoker(request, serviceProvider, payload.Select(p => p.Value).ToArray()));
                }
            }
            else
            {
                JsonParameter[] parameters;
                if (MapJsonServiceCall(serviceProvider, request, out parameters, out methodToCall, out overide))
                {
                    var key = "json_" + GetMethodSignature(request, parameters.Select(p => p.Name).ToArray());
                    Func<HttpRequestMessage, object, object[], HttpResponseMessage> invoker;
                    lock (_invokers)
                    {
                        if (!_invokers.TryGetValue(key, out invoker))
                        {
                            invoker = BuildJsonExecutor(methodToCall, overide);
                            _invokers.Add(key, invoker);
                        }
                    }
                    return AuthorizeAndInvoke(serviceProvider, methodToCall, () => invoker(request, serviceProvider, parameters.Select(p => p.Value).ToArray()));
                }
            }
            throw new MemberAccessException("The method requested with the supplied parameters does not exist");
        }

        private HttpResponseMessage AuthorizeAndInvoke(object serviceProvider, MethodInfo methodToCall, Func<HttpResponseMessage> p)
        {
            var providerAuthorizations = serviceProvider.GetType().GetCustomAttributes<AuthorizeAttribute>();
            var methodAuthorizations = methodToCall.GetCustomAttributes<AuthorizeAttribute>();
            var principal = SecurityContext.Current.CurrentPrincipal;

            try
            {
                if (methodAuthorizations.Count() > 0)
                {
                    var methodPermission = methodAuthorizations.Select(a => a.CreatePermission()).Aggregate((p1, p2) => p1.Union(p2));
                    methodPermission.Demand();
                }
                else if (providerAuthorizations.Count() > 0)
                {
                    var providerPermission = providerAuthorizations.Select(a => a.CreatePermission()).Aggregate((p1, p2) => p1.Union(p2));
                    providerPermission.Demand();
                }

                return p();
            }
            catch (System.Security.SecurityException ex)
            {
                return CreateStringExceptionResponse(ex);
            }
        }

        private bool MapBinaryServiceCall(object serviceProvider, HttpRequestMessage request, out BinaryRequest payload, out MethodInfo methodToCall, out IOverride overide)
        {
            overide = null;
            var contents = request.Content.ReadAsByteArrayAsync().Result;
            payload = new BinaryRequest(new KeyValuePair<string, object>[0]);
            payload.FromBytes(contents);
#if TRACE
            var trace = "Binary Request Contents (base64): " + Convert.ToBase64String(contents);
            Logger.LogInfo(trace.Substring(0, Math.Min(trace.Length, 1024)));
#endif
            var paramCount = payload.Count();
            var methodName = request.RequestUri.Segments.Last();
            var members = serviceProvider.GetType().GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance)
                                     .Where(mi => mi.Name.Equals(methodName, StringComparison.InvariantCultureIgnoreCase))
                                     .ToList();
            methodToCall = null;
            bool isMatch = false;

            if (members.Count == 0)
            {
                methodToCall = null;
                return false;
            }
            else
            {

                bool paramsAreNamed = paramCount > 0 && !(payload.First().Key.StartsWith("p") && payload.First().Key.Contains("|"));
                foreach (var mi in members)
                {
                    isMatch = true;
                    var parms = mi.GetParameters();
                    Argument[] parmTypes = parms.Select(p => new Argument(p.Name, p.ParameterType)).ToArray();
                    if (parms.Length != payload.Count())
                    {
                        var callBuilderAttrib = mi.GetCustomAttribute<OverrideAttribute>();
                        if (callBuilderAttrib != null)
                        {
                            var o = callBuilderAttrib.CreateOverride();
                            overide = o;
                            var parmsO = new List<Argument>();
                            parmsO.AddRange(parmTypes);
                            for (int i = 0; i < overide.ArgumentTypes.Length; i++)
                            {
                                var p = parmsO.FirstOrDefault(pp => pp.Name.Equals(o.ArgumentTypes[i].Name));
                                if (p == null)
                                {
                                    parmsO.Add(overide.ArgumentTypes[i]);
                                }
                                else
                                {
                                    parmsO[parmsO.IndexOf(p)] = overide.ArgumentTypes[i];
                                }
                            }
                            parmTypes = parmsO.ToArray();
                        }
                        else
                        {
                            isMatch = false;
                            continue;
                        }
                    }
                    for (int i = 0; i < parms.Length; i++)
                    {
                        if (paramsAreNamed)
                        {
                            var parameter = payload.SingleOrDefault(p => p.Key.Equals(parmTypes[i].Name, StringComparison.CurrentCultureIgnoreCase));
                            if (parameter.Key == null || !parameter.Key.Equals(parmTypes[i].Name))
                            {
                                isMatch = false;
                                break;
                            }
                        }
                        else
                        {
                            var parameter = payload.SingleOrDefault(p => p.Key.Equals(parmTypes[i].Name, StringComparison.CurrentCultureIgnoreCase));
                            if (!parameter.Value.GetType().Equals(parmTypes[i]))
                            {
                                isMatch = false;
                                break;
                            }
                        }
                    }

                    if (isMatch)
                    {
                        methodToCall = mi;
                        return true;
                    }
                }
            }
            return isMatch;
        }

        private bool MapJsonServiceCall(object serviceProvider, HttpRequestMessage request, out JsonParameter[] parameters, out MethodInfo methodToCall, out IOverride overide)
        {
            var contents = request.Content.ReadAsStringAsync().Result;
            overide = null;
#if TRACE
            Logger.LogInfo("Json Request Contents: " + contents.Substring(0, Math.Min(contents.Length, 1024)));
#endif
            bool paramsAreNamed = false;
            int paramCount = 0;

            parameters = null;
            methodToCall = null;

            if (string.IsNullOrEmpty(contents))
            {
                parameters = new JsonParameter[0];
            }
            else
            {

                var jObject = JObject.Parse(contents);
                var parms = new List<JsonParameter>();

                foreach (var elem in jObject)
                {
                    if (elem.Key.StartsWith("p") && elem.Key.Contains("|"))
                    {
                        // ordinal parameter with embedded type specifier
                        var split = elem.Key.Split('|');
                        var typeName = split[1];
                        var serializedValue = elem.Value;

                        var jsonParam = new JsonParameter()
                        {
                            IsNamed = false,
                            JsonValue = serializedValue.ToString(),
                            Name = split[0],
                            ValueType = Type.GetType(typeName),
                            JsonType = elem.Value.Type
                        };
                        parms.Add(jsonParam);
                    }
                    else
                    {
                        // named parameter without type information
                        paramsAreNamed = true;
                        var jsonParam = new JsonParameter()
                        {
                            IsNamed = true,
                            JsonValue = elem.Value.ToString(),
                            Name = elem.Key,
                            JsonType = elem.Value.Type
                        };
                        parms.Add(jsonParam);
                    }
                }

                parameters = parms.ToArray();
                paramCount = parameters.Length;
            }

            var methodName = request.RequestUri.Segments.Last();
            var members = serviceProvider.GetType().GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance)
                                     .Where(mi => mi.Name.Equals(methodName, StringComparison.InvariantCultureIgnoreCase))
                                     .ToList();

            if (members.Count == 0)
            {
                methodToCall = null;
                return false;
            }
            else
            {

                foreach (var mi in members)
                {
                    bool isMatch = true;
                    var parms = mi.GetParameters();

                    Argument[] parmTypes = parms.Select(p => new Argument(p.Name, p.ParameterType)).ToArray();
                    if (parms.Length != parameters.Length)
                    {
                        var callBuilderAttrib = mi.GetCustomAttribute<OverrideAttribute>();
                        if (callBuilderAttrib != null)
                        {
                            var o = callBuilderAttrib.CreateOverride();
                            overide = o;
                            var parmsO = new List<Argument>();
                            parmsO.AddRange(parmTypes);
                            for (int i = 0; i < overide.ArgumentTypes.Length; i++)
                            {
                                var p = parmsO.FirstOrDefault(pp => pp.Name.Equals(o.ArgumentTypes[i].Name));
                                if (p == null)
                                {
                                    parmsO.Add(overide.ArgumentTypes[i]);
                                }
                                else
                                {
                                    parmsO[parmsO.IndexOf(p)] = overide.ArgumentTypes[i];
                                }
                            }
                            parmTypes = parmsO.ToArray();
                        }
                        else
                        {
                            isMatch = false;
                            continue;
                        }
                    }

                    var orderedParams = new List<JsonParameter>();
                    for (int i = 0; i < parmTypes.Length; i++)
                    {
                        if (paramsAreNamed)
                        {
                            var parameter = parameters.SingleOrDefault(p => p.Name.Equals(parmTypes[i].Name, StringComparison.CurrentCultureIgnoreCase));
                            if (parameter == null)
                            {
                                isMatch = false;
                                orderedParams.Clear();
                                break;
                            }
                            else
                            {
                                parameter.ValueType = parmTypes[i].Type;
                                orderedParams.Add(parameter);
                            }
                        }
                        else
                        {
                            var parameter = parameters.SingleOrDefault(p => p.Name.Equals("p" + i));
                            if (!parameter.ValueType.Equals(parmTypes[i]))
                            {
                                isMatch = false;
                                orderedParams.Clear();
                                break;
                            }
                            else
                            {
                                orderedParams.Add(parameter);
                            }
                        }
                    }

                    if (isMatch)
                    {
                        methodToCall = mi;
                        parameters = orderedParams.ToArray();
                        return true;
                    }
                }
            }
            return false;
        }

        private IList<object> GetArguments(HttpRequestMessage request)
        {
            var contents = request.Content.ReadAsStringAsync().Result;
#if TRACE
            Logger.LogInfo("Request Contents: " + contents.Substring(0, Math.Min(contents.Length, 1024)) + (contents.Length > 1024 ? "..." : ""));
#endif

            if (string.IsNullOrEmpty(contents)) return new List<object>();

            var jObject = JObject.Parse(contents);
            var args = new Dictionary<string, object>();
            foreach (var elem in jObject)
            {
                var typeName = elem.Key.Split('|')[1];
                var serializedValue = elem.Value;

                args.Add(elem.Key, serializedValue.ToString().FromJson(TypeHelper.GetType(typeName)));
            }
            return args.Values.ToList();
        }



        private string GetMethodSignature(HttpRequestMessage request, string[] argumentNames)
        {
            var methodName = request.RequestUri.Segments.Last();
            var service = request.RequestUri.Segments.Skip(request.RequestUri.Segments.Length - 2).Take(1).Single();
            var signature = service + methodName;
            foreach (var arg in argumentNames)
            {
                signature += "&" + arg;
            }
            return signature;
        }


        public Func<HttpRequestMessage, object, object[], HttpResponseMessage> BuildBinaryExecutor(MethodInfo member, IOverride overide)
        {
            var request = Expression.Parameter(typeof(HttpRequestMessage), "request");
            // the object instance whose method will be invoked by the delegate
            var obj = Expression.Parameter(typeof(object), "instance");
            // the arguments passed into the invoked method, as an IList, will be mapped ordinally to the method
            var args = Expression.Parameter(typeof(object[]), "args");
            // the call to the method, with mapped arguments as inputs
            Expression call;
            if (overide == null)
            {
                call = Expression.Call(Expression.Convert(obj, member.DeclaringType), member, MapParameters(args, member.GetParameters()));
            }
            else
            {
                // this call requires a custom invocation, most likely to handle deserialization of arguments for generic method invocation
                call = overide.CreateCall(obj, member, args);
            }

            var responseOK = Expression.New(
                typeof(HttpResponseMessage).GetConstructors(BindingFlags.Public | BindingFlags.Instance).Single(ci => ci.GetParameters().Length == 1),
                Expression.Constant(System.Net.HttpStatusCode.OK));

            var expParam = Expression.Parameter(typeof(Exception));
            //var responseFailed = Expression.New(
            //    typeof(HttpResponseMessage).GetConstructors(BindingFlags.Public | BindingFlags.Instance).Single(ci => ci.GetParameters().Length == 1),
            //    Expression.Constant(System.Net.HttpStatusCode.InternalServerError));
            var responseFailed = Expression.Call(null, typeof(ServiceProviderHandler).GetMethod("CreateBinaryExceptionResponse", BindingFlags.Static | BindingFlags.NonPublic), expParam);

            var r = new HttpResponseMessage();
            Expression execute = null;
            var returnTarget = Expression.Label(typeof(HttpResponseMessage));
            var returnExpFailed = Expression.Return(returnTarget, responseFailed, typeof(HttpResponseMessage));
            var returnLabel = Expression.Label(returnTarget, Expression.Constant(new HttpResponseMessage(System.Net.HttpStatusCode.OK)));

            if (member.ReturnType == typeof(void))
            {
                execute = Expression.Block(call, Expression.Return(returnTarget, responseOK, typeof(HttpResponseMessage)), returnLabel);
            }
            else
            {
                var responseVariable = Expression.Variable(typeof(HttpResponseMessage), "result");
                var response = Expression.Assign(responseVariable, responseOK);
                var serializeObjectMI = typeof(ServiceProviderHandler).GetMethods(BindingFlags.NonPublic | BindingFlags.Static).Single(mi => mi.Name == "BinarySerializeResponse");
                var serializeObjectCall = Expression.Call(null, serializeObjectMI, Expression.Convert(call, typeof(object)));


                var httpMessageContentPI = typeof(HttpResponseMessage).GetProperty("Content", BindingFlags.Public | BindingFlags.Instance);
                var httpMessageContentSetter = httpMessageContentPI.GetSetMethod();

                var stringContentCtor = Expression.Call(null, typeof(ServiceProviderHandler).GetMethod("CreateBinaryResponseContent", BindingFlags.NonPublic | BindingFlags.Static), serializeObjectCall);

                var httpMessageContentSetterExp = Expression.Call(responseVariable, httpMessageContentSetter, stringContentCtor);
                execute = Expression.Block(
                    new ParameterExpression[] { responseVariable },
                    response, httpMessageContentSetterExp, Expression.Return(returnTarget, responseVariable, typeof(HttpResponseMessage)), returnLabel);
            }

            var tryCatch = Expression.TryCatch(
                execute,
                Expression.Catch(expParam,
                    Expression.Block(returnExpFailed, returnLabel))
                );

            var lambda = Expression.Lambda<Func<HttpRequestMessage, object, object[], HttpResponseMessage>>(execute, request, obj, args);
            return lambda.Compile();
        }

        public Func<HttpRequestMessage, object, object[], HttpResponseMessage> BuildJsonExecutor(MethodInfo member, IOverride overide)
        {
            IContractResolver contractResolver = new DefaultContractResolver();
            try
            {
                contractResolver = AppContext.Current.Container.GetInstance<IContractResolver>();
            }
            catch { }
            var jsonSettings = new JsonSerializerSettings() { ContractResolver = contractResolver };

            var request = Expression.Parameter(typeof(HttpRequestMessage));
            // the object instance whose method will be invoked by the delegate
            var obj = Expression.Parameter(typeof(object), "instance");
            // the arguments passed into the invoked method, as an IList, will be mapped ordinally to the method
            var args = Expression.Parameter(typeof(object[]), "args");
            // the call to the method, with mapped arguments as inputs
            Expression call;
            if (overide == null)
            {
                call = Expression.Call(Expression.Convert(obj, member.DeclaringType), member, MapParameters(args, member.GetParameters()));
            }
            else
            {
                // this call requires a custom invocation, most likely to handle deserialization of arguments for generic method invocation
                call = overide.CreateCall(obj, member, args);
            }

            var responseOK = Expression.New(
                typeof(HttpResponseMessage).GetConstructors(BindingFlags.Public | BindingFlags.Instance).Single(ci => ci.GetParameters().Length == 1),
                Expression.Constant(System.Net.HttpStatusCode.OK));
            //var responseFailed = Expression.New(
            //    typeof(HttpResponseMessage).GetConstructors(BindingFlags.Public | BindingFlags.Instance).Single(ci => ci.GetParameters().Length == 1),
            //    Expression.Constant(System.Net.HttpStatusCode.InternalServerError));
            var expParam = Expression.Parameter(typeof(Exception));
            var responseFailed = Expression.Call(null, typeof(ServiceProviderHandler).GetMethod("CreateStringExceptionResponse", BindingFlags.Static | BindingFlags.NonPublic), expParam);

            Expression execute = null;
            var returnTarget = Expression.Label(typeof(HttpResponseMessage));
            var returnExpFailed = Expression.Return(returnTarget, responseFailed, typeof(HttpResponseMessage));
            var returnLabel = Expression.Label(returnTarget, Expression.Constant(new HttpResponseMessage(System.Net.HttpStatusCode.OK)));

            if (member.ReturnType == typeof(void))
            {
                execute = Expression.Block(call, Expression.Return(returnTarget, responseOK, typeof(HttpResponseMessage)), returnLabel);
            }
            else
            {
                var responseVariable = Expression.Variable(typeof(HttpResponseMessage), "response");
                var response = Expression.Assign(responseVariable, responseOK);
                var resultVariable = Expression.Variable(call.Type, "result");
                var result = Expression.Assign(resultVariable, call);
                var getAcceptHeader = GetAcceptHeader(request);
                var acceptHeaderVariable = Expression.Variable(typeof(string), "header");
                var acceptHeaderAssign = Expression.Assign(acceptHeaderVariable, getAcceptHeader);
                var chooseResponse = Expression.Switch(acceptHeaderVariable,
                    CallCreateJsonResponse(resultVariable, response),
                    Expression.SwitchCase(CallCreateJsonResponse(resultVariable, response), Expression.Constant("application/json")),
                    Expression.SwitchCase(CallCreateHtmlResponse(resultVariable, response), Expression.Constant("text/html")),
                    Expression.SwitchCase(CallCreatePdfResponse(resultVariable, response), Expression.Constant("application/pdf")));
                execute = Expression.Block(
                    new ParameterExpression[] { responseVariable, resultVariable, acceptHeaderVariable },
                    result,
                    response,
                    acceptHeaderAssign,
                    chooseResponse,
                    Expression.Return(returnTarget, responseVariable, typeof(HttpResponseMessage)),
                    returnLabel);
            }

            var tryCatch = Expression.TryCatch(
                execute,
                Expression.Catch(typeof(Exception),
                    Expression.Block(returnExpFailed, returnLabel))
                );

            var lambda = Expression.Lambda<Func<HttpRequestMessage, object, object[], HttpResponseMessage>>(execute, request, obj, args);
            return lambda.Compile();
        }

        private static byte[] BinarySerializeResponse(object value)
        {
            if (value == null)
                return new byte[0];
            else if (value is IBinarySerializable)
                return ((IBinarySerializable)value).ToBytes();
            else
            {
                var serializer = SerializationContext.Instance.GetSerializer(value.GetType(), StandardFormats.BINARY);
                return serializer.Serialize(value);
            }
        }

        private static HttpResponseMessage CreateBinaryExceptionResponse(Exception ex)
        {
            Logger.LogError(ex);
            var response = new HttpResponseMessage(System.Net.HttpStatusCode.InternalServerError);
#if(DEBUG)
            response.Content = CreateBinaryResponseContent(SerializationContext.Instance.TextEncoding.GetBytes(ex.ToString()));
#else
            response.Content = CreateBinaryResponseContent(SerializationContext.Instance.TextEncoding.GetBytes(ex.Message));
#endif
            return response;
        }

        private static ByteArrayContent CreateBinaryResponseContent(byte[] bytes)
        {
#if (TRACE)
            var content = Convert.ToBase64String(bytes);
            Logger.LogInfo("Binary Response Content (base64): " + content.Substring(0, Math.Min(content.Length, 1024)) + (content.Length > 1024 ? "..." : ""));
#endif
            return new ByteArrayContent(bytes);
        }

        private Expression CallCreatePdfResponse(ParameterExpression resultVariable, BinaryExpression response)
        {
            var callMethod = typeof(ServiceProviderHandler).GetMethod("CreatePdfResponse", BindingFlags.NonPublic | BindingFlags.Static);
            return Expression.Call(null, callMethod, Expression.Convert(resultVariable, typeof(object)), response);
        }

        private Expression CallCreateHtmlResponse(ParameterExpression resultVariable, BinaryExpression response)
        {
            var callMethod = typeof(ServiceProviderHandler).GetMethod("CreateHtmlResponse", BindingFlags.NonPublic | BindingFlags.Static);
            return Expression.Call(null, callMethod, Expression.Convert(resultVariable, typeof(object)), response);
        }

        private Expression CallCreateJsonResponse(ParameterExpression resultVariable, BinaryExpression response)
        {
            var callMethod = typeof(ServiceProviderHandler).GetMethod("CreateJsonResponse", BindingFlags.NonPublic | BindingFlags.Static);
            return Expression.Call(null, callMethod, Expression.Convert(resultVariable, typeof(object)), response);
        }

        private Expression GetAcceptHeader(ParameterExpression request)
        {
            // r.Headers.GetValues("Accept").FirstOrDefault();
            var getHeadersGetter = typeof(HttpRequestMessage).GetProperty("Headers").GetMethod;
            var getHeadersCall = Expression.Call(request, getHeadersGetter);
            var getValuesMethod = typeof(HttpRequestHeaders).GetMethod("GetValues", BindingFlags.Public | BindingFlags.Instance);
            var getValuesCall = Expression.Call(getHeadersCall, getValuesMethod, Expression.Constant("Accept"));
            var firstMethod = typeof(Enumerable).GetMethods(BindingFlags.Public | BindingFlags.Static)
                                                .First(mi => mi.Name.Equals("First") && mi.GetParameters().Length == 1)
                                                .MakeGenericMethod(typeof(string));
            var countMethod = typeof(Enumerable).GetMethods(BindingFlags.Public | BindingFlags.Static)
                                                .First(mi => mi.Name.Equals("Count") && mi.GetParameters().Length == 1)
                                                .MakeGenericMethod(typeof(string));
            var valuesVariable = Expression.Variable(typeof(IEnumerable<string>), "headers");
            var valuesAssign = Expression.Assign(valuesVariable, getValuesCall);
            var result = Expression.Variable(typeof(string), "result");

            var conditon = Expression.IfThenElse(
                Expression.OrElse(
                                    Expression.Equal(valuesVariable, Expression.Constant(null)),
                                    Expression.Equal(Expression.Call(null, countMethod, valuesVariable), Expression.Constant(0))
                                 ),
                Expression.Assign(result, Expression.Constant("application/json")),
                Expression.Assign(result, Expression.Call(null, firstMethod, valuesVariable))
                );

            var returnTarget = Expression.Label(typeof(string));
            var returnLabel = Expression.Label(returnTarget, Expression.Constant("application/json"));


            return Expression.Block(
                        new ParameterExpression[] { valuesVariable, result },
                        valuesAssign,
                        conditon,
                        Expression.Return(returnTarget, result, typeof(string)),
                        returnLabel);
        }

        //        private static StringContent CreateResponseContent(string content)
        //        {
        //#if (TRACE)
        //            Logger.LogInfo("Response Content: " + content.Substring(0, Math.Min(content.Length, 1024)) + (content.Length > 1024 ? "..." : ""));
        //#endif
        //            return new StringContent(content);
        //        }
        private static HttpResponseMessage CreateStringExceptionResponse(Exception ex)
        {
            Logger.LogError(ex);
            var response = new HttpResponseMessage(System.Net.HttpStatusCode.InternalServerError);
#if (DEBUG)
            CreateJsonResponse(ex, response);
#else
            CreateJsonResponse(new { Message = ex.Message }, response);
#endif
            return response;
        }

        private static HttpResponseMessage CreateJsonResponse(object content, HttpResponseMessage response)
        {
            var jsonBody = content.ToJson();
#if (TRACE)
            Logger.LogInfo("Response Content: " + jsonBody.Substring(0, Math.Min(jsonBody.Length, 1024)) + (jsonBody.Length > 1024 ? "..." : ""));
#endif
            response.Content = new StringContent(jsonBody, UTF8Encoding.UTF8, "application/json");
            return response;
        }

        private static HttpResponseMessage CreatePdfResponse(object content, HttpResponseMessage response)
        {
            var body = content is string ? content.ToString() : content.ToJson();
            body = body.Trim();

#if (TRACE)
            Logger.LogInfo("Response Content: " + body.Substring(0, Math.Min(body.Length, 1024)) + (body.Length > 1024 ? "..." : ""));
#endif
            if (!body.StartsWith("<html>") && !body.StartsWith("<!DOCTYPE html>"))
            {
                body = WebUtility.HtmlEncode(body);
            }


            response.Content = new ByteArrayContent(CreatePdf(body));
            response.Content.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
            response.Content.Headers.ContentDisposition = new ContentDispositionHeaderValue("attachment") { FileName = "response.pdf" };
            return response;

        }

        private static byte[] CreatePdf(string value)
        {
            //var pdf = PdfGenerator.GeneratePdf(value, PdfSharp.PageSize.Letter);
            //using (var ms = new MemoryStream())
            //{
            //    pdf.Save(ms);
            //    return ms.ToArray();
            //}
            var converter = new HtmlToPdf();
            converter.BrowserWidth = 800;
            converter.Document.PageSize = PdfPageSize.Letter;
            converter.Document.PageOrientation = PdfPageOrientation.Portrait;
            converter.Document.PdfStandard = PdfStandard.Pdf;
            converter.Document.Margins = new PdfMargins(5);
            return converter.ConvertHtmlToMemory(value, "");
        }

        private static HttpResponseMessage CreateHtmlResponse(object content, HttpResponseMessage response)
        {
            var jsonBody = content is string ? content.ToString() : content.ToJson();
            if (jsonBody.StartsWith("\""))
            {
                jsonBody = jsonBody.Substring(1, jsonBody.Length - 2);
            }
            jsonBody = jsonBody.Trim();
            if (!jsonBody.StartsWith("<!DOCTYPE html>"))
            {
                jsonBody = "<!DOCTYPE html>" + System.Environment.NewLine + jsonBody;
            }

#if (TRACE)
            Logger.LogInfo("Response Content: " + jsonBody.Substring(0, Math.Min(jsonBody.Length, 1024)) + (jsonBody.Length > 1024 ? "..." : ""));
#endif
            response.Content = new StringContent(jsonBody, UTF8Encoding.UTF8, "text/html");
            return response;
        }

        private IEnumerable<Expression> MapParameters(ParameterExpression args, ParameterInfo[] parms)
        {
            for (int p = 0; p < parms.Length; p++)
            {
                // read element p from the passed in IList<object> parameter, args.
                yield return Expression.Convert(Expression.ArrayAccess(args, Expression.Constant(p)), parms[p].ParameterType);
            }
        }
    }
}

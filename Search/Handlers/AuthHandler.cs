using Common.Web.Handlers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net.Http;
using System.Threading;
using System.Net;
using System.IO;
using Newtonsoft.Json.Linq;
using Data;
using Suffuz.Data;
using Common;
using Data.Core;
using Data.Core.Security;
using Common.Diagnostics;
using Common.Security;

namespace Suffuz.Handlers
{
    public class AuthHandler : IDelegatingHandler
    {
        public Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var code = request.RequestUri.ParseQueryString()["code"];
            Logger.LogInfo("Received Team Auth Code: " + code + "; RequestUri: " + request.RequestUri.ToString());
            return Task.Run(() => GetToken(code));
        }

        public HttpResponseMessage GetToken(string code)
        {
            var tokenUri = "https://slack.com/api/oauth.access";
            /*
            REQUEST
            client_id - issued when you created your app (required)
            client_secret - issued when you created your app (required)
            code - a temporary authorization code (required)
            redirect_uri - must match the originally submitted URI (if one was sent) 
              
            RESPONSE
            {
                "access_token": "xoxp-XXXXXXXX-XXXXXXXX-XXXXX",
                "scope": "incoming-webhook,commands,bot",
                "team_name": "Team Installing Your Hook",
                "team_id": "XXXXXXXXXX",
                "incoming_webhook": {
                    "url": "https://hooks.slack.com/TXXXXX/BXXXXX/XXXXXXXXXX",
                    "channel": "#channel-it-will-post-to",
                    "configuration_url": "https://teamname.slack.com/services/BXXXXX"
                },
                "bot":{
                    "bot_user_id":"UTTTTTTTTTTR",
                    "bot_access_token":"xoxb-XXXXXXXXXXXX-TTTTTTTTTTTTTT"
                }
            }

            */

            var clientId = AppContext.GetEnvironmentVariable("slack:client_id","");
            var clientSecret = AppContext.GetEnvironmentVariable("slack:client_secret", "");
            var redirectUri = AppContext.GetEnvironmentVariable("slack:redirect_uri", "");

            try
            {
                var request = HttpWebRequest.CreateHttp(string.Format("{0}?client_id={1}&client_secret={2}&code={3}&redirect_uri={4}",
                    tokenUri,
                    clientId,
                    clientSecret,
                    code,
                    redirectUri));

                using (var response = request.GetResponse())
                {
                    using (var stream = response.GetResponseStream())
                    {
                        using (var sr = new StreamReader(stream))
                        {
                            var json = sr.ReadToEnd();
                            
                            var result = new  HttpResponseMessage(HttpStatusCode.OK);
                            result.Content = new StringContent(RegisterTeamToken(json));
                            return result;
                        }
                    }
                }
            }
            catch
            {
                return new HttpResponseMessage(HttpStatusCode.InternalServerError);
            }
        }

        protected virtual string RegisterTeamToken(string json)
        {
            var principal = SecurityContext.Current.CurrentPrincipal;
            try
            {
                SecurityContext.Current.CurrentPrincipal = IUserDefaults.Administrator;
                var jObj = JObject.Parse(json);
                var teamId = jObj.Property("team_id").Value.ToString();

                var appTokenQP = AppContext.Current.Container.GetInstance<IModelQueryProviderBuilder>().CreateQueryProvider<IAppToken>();

                IAppToken token = appTokenQP.Query(string.Format("{0}{{TeamId = '{1}'}}", ModelTypeManager.GetModelName<IAppToken>(), teamId)).Cast<IAppToken>().FirstOrDefault();

                if (token == null)
                    token = Model.New<IAppToken>();

                token.Token = jObj.Property("access_token").Value?.ToString();
                token.Scope = jObj.Property("scope").Value?.ToString();
                token.UserId = jObj.Property("user_id")?.Value?.ToString();
                token.TeamId = teamId;
                token.TeamName = jObj.Property("team_name")?.Value?.ToString();
                JToken incomingWebHook;
                if (jObj.TryGetValue("incoming_webhook", out incomingWebHook))
                {
                    var wh = Model.New<IWebHook>();
                    wh.Url = ((JObject)incomingWebHook).Property("url")?.Value?.ToString();
                    wh.Channel = ((JObject)incomingWebHook).Property("channel")?.Value?.ToString();
                    wh.ConfigurationUrl = ((JObject)incomingWebHook).Property("configuration_url")?.Value?.ToString();
                    token.IncomingWebHook = wh;
                }
                JToken bot;
                if (jObj.TryGetValue("bot", out bot))
                {
                    var b = Model.New<IBotUserToken>();
                    b.UserId = ((JObject)incomingWebHook).Property("bot_user_id")?.Value?.ToString();
                    b.Token = ((JObject)incomingWebHook).Property("bot_access_token")?.Value?.ToString();
                    token.BotUserToken = b;
                }
                var appTokenPP = AppContext.Current.Container.GetInstance<IModelPersistenceProviderBuilder>().CreatePersistenceProvider<IAppToken>();
                if (token.IsNew)
                {
                    var ouQP = AppContext.Current.Container.GetInstance<IModelQueryProviderBuilder>().CreateQueryProvider<IOrgUnit>();
                    var ou = ouQP.Query(string.Format("{0}{{Name = '{1}'}}", ModelTypeManager.GetModelName<IOrgUnit>(), token.TeamName)).Cast<IOrgUnit>().FirstOrDefault();
                    if (ou == null)
                    {
                        var ouI = AppContext.Current.Container.GetInstance<IOrgUnitInitializer>();
                        ou = ouI.Create(token.TeamId, token.TeamName);
                    }

                    return NewTokenResponse(appTokenPP.Create(token, ou));
                }
                else
                {
                    return UpdatedTokenResponse(appTokenPP.Update(token));
                }
            }
            finally
            {
                SecurityContext.Current.CurrentPrincipal = principal;
            }
        }

        private string NewTokenResponse(IAppToken appToken)
        {
            return string.Format("Welcome to Suffuz, {0}!", appToken.TeamName);
        }

        private string UpdatedTokenResponse(IAppToken appToken)
        {
            return string.Format("Your token has been updated, {0}!", appToken.TeamName);
        }
    }
}

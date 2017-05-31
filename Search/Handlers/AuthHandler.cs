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
            var userProfileUri = "https://slack.com/api/users.info?token={0}&user={1}";
            /*

            {
              "ok": true,
              "user": {
                "id": "U56TJC2A2",
                "team_id": "T56TWDVEK",
                "name": "odinhaus",
                "deleted": false,
                "color": "9f69e7",
                "real_name": "Bill Blondin",
                "tz": "America/Chicago",
                "tz_label": "Central Daylight Time",
                "tz_offset": -18000,
                "profile": {
                  "first_name": "Bill",
                  "last_name": "Blondin",
                  "avatar_hash": "e18ad4aa2287",
                  "image_24": "https://avatars.slack-edge.com/2017-04-28/175462465760_e18ad4aa228726b35382_24.png",
                  "image_32": "https://avatars.slack-edge.com/2017-04-28/175462465760_e18ad4aa228726b35382_32.png",
                  "image_48": "https://avatars.slack-edge.com/2017-04-28/175462465760_e18ad4aa228726b35382_48.png",
                  "image_72": "https://avatars.slack-edge.com/2017-04-28/175462465760_e18ad4aa228726b35382_72.png",
                  "image_192": "https://avatars.slack-edge.com/2017-04-28/175462465760_e18ad4aa228726b35382_192.png",
                  "image_512": "https://avatars.slack-edge.com/2017-04-28/175462465760_e18ad4aa228726b35382_512.png",
                  "image_1024": "https://avatars.slack-edge.com/2017-04-28/175462465760_e18ad4aa228726b35382_1024.png",
                  "image_original": "https://avatars.slack-edge.com/2017-04-28/175462465760_e18ad4aa228726b35382_original.png",
                  "phone": "832.381.8488",
                  "real_name": "Bill Blondin",
                  "real_name_normalized": "Bill Blondin"
                },
                "is_admin": true,
                "is_owner": true,
                "is_primary_owner": true,
                "is_restricted": false,
                "is_ultra_restricted": false,
                "is_bot": false,
                "updated": 1493389056,
                "has_2fa": false
              }
            }

            */
            var clientId = AppContext.GetEnvironmentVariable("slack_client_id","");
            var clientSecret = AppContext.GetEnvironmentVariable("slack_client_secret", "");
            var redirectUri = AppContext.GetEnvironmentVariable("slack_redirect_uri", "");

            try
            {
                var request = HttpWebRequest.CreateHttp(string.Format("{0}?client_id={1}&client_secret={2}&code={3}&redirect_uri={4}",
                    tokenUri,
                    clientId,
                    clientSecret,
                    code,
                    redirectUri));

                IAppToken token;
                string tokenMessage;
                using (var response = request.GetResponse())
                {
                    using (var stream = response.GetResponseStream())
                    {
                        using (var sr = new StreamReader(stream))
                        {
                            var json = sr.ReadToEnd();
                            // TODO: handle faults
                            tokenMessage = RegisterTeamToken(json, out token);
                        }
                    }
                }

                request = HttpWebRequest.CreateHttp(string.Format(userProfileUri, token.Token, token.UserId));
                string username, firstName, lastName, email;

                using (var response = request.GetResponse())
                {
                    using (var stream = response.GetResponseStream())
                    {
                        using (var sr = new StreamReader(stream))
                        {
                            var json = sr.ReadToEnd();
                            ReadProfile(json, out username, out firstName, out lastName, out email);
                        }
                    }
                }


                // get the user's profile from https://slack.com/api/users.profile.get?token=xoxp-176948471495-176936410342-186937474071-5681777a245854d84edb4a8e179472ab&user=U56TJC2A2

                var result = new HttpResponseMessage(HttpStatusCode.Found);
                result.Headers.Add("Location", AppContext.GetEnvironmentVariable("WebUri", "") 
                    + "/#/signup?tokenKey=" + token.Key.ToString() 
                    + "&teamName=" + token.TeamName 
                    + "&userId=" + token.UserId
                    + "&userName=" + username
                    + "&firstName=" + firstName
                    + "&lastName=" + lastName
                    + "&email=" + email);
                return result;
            }
            catch
            {
                return new HttpResponseMessage(HttpStatusCode.InternalServerError);
            }
        }

        private void ReadProfile(string json, out string username, out string firstName, out string lastName, out string email)
        {
            var jObj = JObject.Parse(json);
            var jUser = jObj.Property("user").Value as JObject;
            username = jUser.Property("name").Value.ToString();
            var jProfile = jUser.Property("profile").Value as JObject;
            firstName = jProfile.Property("first_name").Value.ToString();
            lastName = jProfile.Property("last_name").Value.ToString();
            email = jProfile.Property("email")?.Value?.ToString();
        }

        protected virtual string RegisterTeamToken(string json, out IAppToken token)
        {
            var principal = SecurityContext.Current.CurrentPrincipal;
            try
            {
                SecurityContext.Current.CurrentPrincipal = IUserDefaults.Administrator;
                var jObj = JObject.Parse(json);
                var teamId = jObj.Property("team_id").Value.ToString();

                var appTokenQP = AppContext.Current.Container.GetInstance<IModelQueryProviderBuilder>().CreateQueryProvider<IAppToken>();

                token = appTokenQP.Query(string.Format("{0}{{TeamId = '{1}'}}", ModelTypeManager.GetModelName<IAppToken>(), teamId)).Cast<IAppToken>().FirstOrDefault();

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
                    b.UserId = ((JObject)bot).Property("bot_user_id")?.Value?.ToString();
                    b.Token = ((JObject)bot).Property("bot_access_token")?.Value?.ToString();
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
                    token = appTokenPP.Create(token, ou);
                    return NewTokenResponse(token);
                }
                else
                {
                    token = appTokenPP.Update(token);
                    return UpdatedTokenResponse(token);
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

namespace Suffuz.Identity.Controllers
{
    using AspNet.Identity.MongoDB;
    using Common.Security;
    using Entities;
    using Microsoft.AspNet.Identity;
    using Microsoft.AspNet.Identity.Owin;
    using Microsoft.Owin.Security;
    using Microsoft.Owin.Security.OAuth;
    using Models;
    using Newtonsoft.Json.Linq;
    using Results;
    using System;
    using System.Collections.Generic;
    using System.Configuration;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Net.Http;
    using System.Security.Claims;
    using System.Text.RegularExpressions;
    using System.Threading.Tasks;
    using System.Web.Http;

    [RoutePrefix("api/Account")]
    public class AccountController : ApiController
    {
        private readonly AuthRepository authRepository = null;

        public AccountController(AuthRepository authRepository)
        {
            this.authRepository = authRepository;
        }

        // POST api/Account/Register
        [AllowAnonymous]
        [HttpPost]
        [Route("Register")]
        public async Task<IHttpActionResult> Register(UserModel userModel)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var result = await authRepository.RegisterUser(userModel);

            var errorResult = GetErrorResult(result);

            if (errorResult != null)
            {
                return errorResult;
            }

            return Ok();
        }

        public ApplicationUserManager UserManager
        {
            get
            {
                return Request.GetOwinContext().GetUserManager<ApplicationUserManager>();
            }
        }

        [AllowAnonymous]
        [HttpPost]
        [Route("customer/register")]
        public IHttpActionResult RegisterCustomer(CustomerModel customerModel)
        {
            var customer = new Customer { Name = customerModel.Name, Prefix = customerModel.Prefix };
            var serviceResponse = authRepository.RegisterCustomer(customer);

            return Ok(serviceResponse);
        }

        private IAuthenticationManager Authentication
        {
            get { return Request.GetOwinContext().Authentication; }
        }

        // GET api/Account/ExternalLogin
        [OverrideAuthentication]
        [HostAuthentication(DefaultAuthenticationTypes.ExternalCookie)]
        [AllowAnonymous]
        [Route("ExternalLogin", Name = "ExternalLogin")]
        public async Task<IHttpActionResult> GetExternalLogin(string provider, string customer_key, string device_id, string error = null)
        {
            string redirectUri = string.Empty;

            if (error != null)
            {
                return BadRequest(Uri.EscapeDataString(error));
            }

            if (!(User?.Identity?.IsAuthenticated ?? false))
            {
                return new ChallengeResult(provider, this);
            }

            var redirectUriValidationResult = ValidateClientAndRedirectUri(this.Request, ref redirectUri);

            if (!string.IsNullOrWhiteSpace(redirectUriValidationResult))
            {
                return BadRequest(redirectUriValidationResult);
            }

            var externalLogin = ExternalLoginData.FromIdentity(User.Identity as ClaimsIdentity);

            if (externalLogin == null)
            {
                return InternalServerError();
            }

            if (externalLogin.LoginProvider != provider)
            {
                Authentication.SignOut(DefaultAuthenticationTypes.ExternalCookie);
                return new ChallengeResult(provider, this);
            }

            var user = await authRepository.FindAsync(new UserLoginInfo(externalLogin.LoginProvider, externalLogin.ProviderKey));

            bool hasRegistered = user != null;

            redirectUri = string.Format("{0}?external_access_token={1}&provider={2}&haslocalaccount={3}&external_user_name={4}&customer_key={5}&client_id={6}&device_id={7}",
                                            redirectUri,
                                            externalLogin.ExternalAccessToken,
                                            externalLogin.LoginProvider,
                                            hasRegistered.ToString(),
                                            externalLogin.UserName,
                                            customer_key,
                                            GetQueryString(Request, "client_id"),
                                            GetQueryString(Request, "device_id"));

            return Redirect(redirectUri);

        }

        // GET api/Account/ExternalLogin
        [OverrideAuthentication]
        [HostAuthentication(DefaultAuthenticationTypes.ExternalCookie)]
        [AllowAnonymous]
        [Route("ExternalAuthComplete", Name = "ExternalAuthComplete")]
        public IHttpActionResult GetExternalAuthComplete(string external_access_token, string provider, bool hasLocalAccount, string external_user_name, string customer_key, string client_id, string device_id)
        {
            string redirectUri = null;
            var rootUri = Request.RequestUri.OriginalString.Replace(Request.RequestUri.PathAndQuery, "");
            if (hasLocalAccount)
            {
                redirectUri = string.Format("{0}?external_access_token={1}&provider={2}&customer_key={3}&client_id={4}&device_id={5}",
                                            rootUri + "/api/Account/ObtainLocalAccessToken",
                                            external_access_token,
                                            provider,
                                            customer_key,
                                            client_id,
                                            device_id);
            }
            else
            { 
                redirectUri = string.Format("{0}?external_access_token={1}&provider={2}&external_user_name={3}&customer_key={4}&client_id={5}&device_id={6}",
                                            rootUri + "/api/Account/RegisterExternal",
                                            external_access_token,
                                            provider,
                                            external_user_name,
                                            customer_key,
                                            client_id,
                                            device_id);
            }

            return Redirect(redirectUri);
        }

        [HostAuthentication(DefaultAuthenticationTypes.ExternalBearer)]
        [Route("ChangePassword")]
        public IHttpActionResult ChangePassword(ChangePasswordBindingModel model)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            if (User == null || !User.Identity.IsAuthenticated)
            {
                return GetErrorResult(new IdentityResult("Authentication failed"));
            }

            var user = UserManager.FindByName(User.Identity.Name);
            var result = UserManager.ChangePassword(user.Id, model.OldPassword, model.NewPassword);

            if (!result.Succeeded)
            {
                return GetErrorResult(result);
            }

            return Ok();
        }

        private string ValidateClientAndRedirectUri(HttpRequestMessage request, ref string redirectUriOutput)
        {
            Uri redirectUri;

            var redirectUriString = GetQueryString(Request, "redirect_uri");

            if (string.IsNullOrWhiteSpace(redirectUriString))
            {
                return "redirect_uri is required";
            }

            bool validUri = Uri.TryCreate(redirectUriString, UriKind.Absolute, out redirectUri);

            if (!validUri)
            {
                return "redirect_uri is invalid";
            }

            var clientId = GetQueryString(Request, "client_id");

            if (string.IsNullOrWhiteSpace(clientId))
            {
                return "client_Id is required";
            }

            var client = authRepository.FindClient(clientId);

            if (client == null)
            {
                return string.Format("Client_id '{0}' is not registered in the system.", clientId);
            }

            if (!string.Equals(client.AllowedOrigin, redirectUri.GetLeftPart(UriPartial.Authority), StringComparison.OrdinalIgnoreCase))
            {
                return string.Format("The given URL is not allowed by Client_id '{0}' configuration.", clientId);
            }

            redirectUriOutput = redirectUri.AbsoluteUri;

            return string.Empty;

        }

        private string GetQueryString(HttpRequestMessage request, string key)
        {
            var queryStrings = request.GetQueryNameValuePairs();

            if (queryStrings == null) return null;

            var match = queryStrings.FirstOrDefault(keyValue => string.Compare(keyValue.Key, key, true) == 0);

            if (string.IsNullOrEmpty(match.Value)) return null;

            return match.Value;
        }

        private async Task<ParsedExternalAccessToken> VerifyExternalAccessToken(string provider, string accessToken)
        {
            ParsedExternalAccessToken parsedToken = null;

            var verifyTokenEndPoint = "";

            if (provider == "Facebook")
            {
                //You can get it from here: https://developers.facebook.com/tools/accesstoken/
                //More about debug_tokn here: http://stackoverflow.com/questions/16641083/how-does-one-get-the-app-access-token-for-debug-token-inspection-on-facebook

                var appToken = ConfigurationManager.AppSettings["fb_appToken"];
                verifyTokenEndPoint = string.Format("https://graph.facebook.com/debug_token?input_token={0}&access_token={1}", accessToken, appToken);
            }
            else if (provider == "Google")
            {
                verifyTokenEndPoint = string.Format("https://www.googleapis.com/oauth2/v1/tokeninfo?access_token={0}", accessToken);
            }
            else
            {
                return null;
            }

            var client = new HttpClient();
            var uri = new Uri(verifyTokenEndPoint);
            var response = await client.GetAsync(uri);

            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();

                var jObj = (JObject)Newtonsoft.Json.JsonConvert.DeserializeObject(content);

                parsedToken = new ParsedExternalAccessToken();

                if (provider == "Facebook")
                {
                    parsedToken.user_id = ((JObject)jObj.GetValue("data")).GetValue("user_id").Value<string>();
                    parsedToken.app_id = ((JObject)jObj.GetValue("data")).GetValue("app_id").Value<string>();

                    if (!string.Equals(Startup.FacebookAuthOptions.AppId, parsedToken.app_id, StringComparison.OrdinalIgnoreCase))
                    {
                        return null;
                    }
                }
                else if (provider == "Google")
                {
                    parsedToken.user_id = jObj.GetValue("user_id").Value<string>();
                    parsedToken.app_id = jObj.GetValue("audience").Value<string>();

                    if (!string.Equals(Startup.GoogleAuthOptions.ClientId, parsedToken.app_id, StringComparison.OrdinalIgnoreCase))
                    {
                        return null;
                    }

                }

            }

            return parsedToken;
        }

        private TokenResponse GenerateLocalAccessTokenResponse(string userName, string customerName, string customerId, string clientId, string deviceId, string provider)
        {
            ClaimsIdentity identity = new ClaimsIdentity(OAuthDefaults.AuthenticationType);

            identity.AddClaim(new Claim(ClaimTypes.Name, userName));
            identity.AddClaim(new Claim("role", "user"));
            identity.AddClaim(new Claim("customer_id", customerId, ClaimValueTypes.String, "LOCAL AUTHORITY", "LOCAL AUTHORITY"));
            identity.AddClaim(new Claim("customer_name", customerName, ClaimValueTypes.String, "LOCAL AUTHORITY", "LOCAL AUTHORITY"));
            identity.AddClaim(new Claim(ClaimTypesEx.DeviceId, deviceId, ClaimValueTypes.String, "LOCAL AUTHORITY", "LOCAL AUTHORITY"));
            identity.AddClaim(new Claim("provider", provider, ClaimValueTypes.String, "LOCAL AUTHORITY", "LOCAL AUTHORITY"));

            var props = new AuthenticationProperties()
            {
                IssuedUtc = DateTime.UtcNow,
                ExpiresUtc = DateTime.UtcNow.Add(Startup.OAuthOptions.AccessTokenExpireTimeSpan),
            };

            var ticket = new AuthenticationTicket(identity, props);

            var accessToken = Startup.OAuthBearerOptions.AccessTokenFormat.Protect(ticket);

            //return tokenResponse;
            var claims = identity.Claims.Select(c => new SerializableClaim()
            {
                Issuer = c.Issuer,
                OriginalIssuer = c.OriginalIssuer,
                Type = c.Type,
                Value = c.Value,
                ValueType = c.ValueType
            })
            .Union(GetExternalClaims(authRepository.FindClient(clientId).ClaimsUri, identity, props))
            .ToArray();

            var tokenResponse = new TokenResponse()
            {
                Username = userName,
                AccessToken = accessToken,
                TokenType = "bearer",
                ExpiresIn = (int)Startup.OAuthOptions.AccessTokenExpireTimeSpan.TotalSeconds,
                IssuedAt = ticket.Properties.IssuedUtc.ToString(),
                ExpiresAt = ticket.Properties.ExpiresUtc.ToString(),
                CustomerId = customerId,
                CustomerName = customerName,
                Claims = claims
            };
            return tokenResponse;
        }

        private IEnumerable<SerializableClaim> GetExternalClaims(string claimsUri, ClaimsIdentity identity, AuthenticationProperties props)
        {
            if (!string.IsNullOrEmpty(claimsUri))
            {
                // generate a temporary ticket to make the 
                var ticket = new AuthenticationTicket(identity, props);
                ticket.Properties.ExpiresUtc = DateTime.UtcNow.AddMinutes(5);
                ticket.Properties.IssuedUtc = DateTime.UtcNow;
                ticket.Properties.IsPersistent = false;
                ticket.Properties.AllowRefresh = false;
                // create a bearer token to assign to the call
                var accessToken = Startup.OAuthBearerOptions.AccessTokenFormat.Protect(ticket);

                var request = HttpWebRequest.CreateHttp(claimsUri);
                request.Method = "GET";
                request.Headers.Add("Authorization", "Bearer " + accessToken);
                request.Accept = "application/json";
                using (var response = request.GetResponse())
                {
                    using (var sr = new StreamReader(response.GetResponseStream()))
                    {
                        var body = sr.ReadToEnd();
                        if (!string.IsNullOrEmpty(body))
                        {
                            return JArray.Parse(body).ToObject<SerializableClaim[]>();
                        }
                    }
                }
            }
            return new SerializableClaim[0];
        }

        // GET api/Account/RegisterExternal
        [AllowAnonymous]
        [HttpGet]
        [Route("RegisterExternal")]
        public async Task<IHttpActionResult> RegisterExternal(string external_access_token, string provider, string external_user_name, string customer_key, string client_id, string device_id)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var verifiedAccessToken = await VerifyExternalAccessToken(provider, external_access_token);
            if (verifiedAccessToken == null)
            {
                return BadRequest("Invalid Provider or External Access Token");
            }

            var user = (User)await authRepository.FindAsync(new UserLoginInfo(provider, verifiedAccessToken.user_id));
            var customer = authRepository.FindCustomer(customer_key);

            bool hasRegistered = user != null;

            if (hasRegistered)
            {
                return BadRequest("External user is already registered");
            }
            var cleanName = Regex.Replace(external_user_name, "[^0-9A-Za-z]", "_");
            user = new User() { UserName = string.Format("{0}@{1}", cleanName, provider), CustomerId = customer_key };

            IdentityResult result = await authRepository.CreateAsync(user);
            if (!result.Succeeded)
            {
                return GetErrorResult(result);
            }

            var info = new ExternalLoginInfo()
            {
                DefaultUserName = external_user_name,
                Login = new UserLoginInfo(provider, verifiedAccessToken.user_id)
            };

            result = await authRepository.AddLoginAsync(user.Id, info.Login);
            if (!result.Succeeded)
            {
                return GetErrorResult(result);
            }

            //generate access token response
            var accessTokenResponse = GenerateLocalAccessTokenResponse(external_user_name, customer.Name, customer_key, client_id, device_id, provider);

            return Json(accessTokenResponse);
        }

        [AllowAnonymous]
        [HttpGet]
        [Route("ObtainLocalAccessToken")]
        public async Task<IHttpActionResult> ObtainLocalAccessToken(string provider, string external_access_token, string customer_key, string client_id, string device_id)
        {

            if (string.IsNullOrWhiteSpace(provider) || string.IsNullOrWhiteSpace(external_access_token))
            {
                return BadRequest("Provider or external access token is not sent");
            }

            var verifiedAccessToken = await VerifyExternalAccessToken(provider, external_access_token);
            if (verifiedAccessToken == null)
            {
                return BadRequest("Invalid Provider or External Access Token");
            }

            IdentityUser user = await authRepository.FindAsync(new UserLoginInfo(provider, verifiedAccessToken.user_id));
            var customer = authRepository.FindCustomer(customer_key);

            bool hasRegistered = user != null;

            if (!hasRegistered)
            {
                return BadRequest("External user is not registered");
            }

            //generate access token response
            var accessTokenResponse = GenerateLocalAccessTokenResponse(user.UserName, customer.Name, customer_key, client_id, device_id, provider);

            return Json(accessTokenResponse);

        }

        //public IHttpActionResult Get() {

        //    var result =  new
        //    {
        //        IP = HttpContext.Current.Request.UserHostAddress,
        //        HostName =    HttpContext.Current.Request.UserHostName,
        //        Url = HttpContext.Current.Request.Url.Host,
        //        XOriginalURL = HttpContext.Current.Request.Headers.GetValues("X-Original-URL"),
        //        HeaderKeys = HttpContext.Current.Request.Headers.AllKeys,
        //        Origin = HttpContext.Current.Request.Headers.GetValues("Origin")
        //    };

        //    return Ok(result);
        //}

        private IHttpActionResult GetErrorResult(IdentityResult result)
        {
            if (result == null)
            {
                return InternalServerError();
            }

            if (!result.Succeeded)
            {
                if (result.Errors != null)
                {
                    foreach (string error in result.Errors)
                    {
                        ModelState.AddModelError("", error);
                    }
                }

                if (ModelState.IsValid)
                {
                    // No ModelState errors are available to send, so just return an empty BadRequest.
                    return BadRequest();
                }

                return BadRequest(ModelState);
            }

            return null;
        }

        private class ExternalLoginData
        {
            public string LoginProvider { get; set; }
            public string ProviderKey { get; set; }
            public string UserName { get; set; }
            public string ExternalAccessToken { get; set; }

            public static ExternalLoginData FromIdentity(ClaimsIdentity identity)
            {
                if (identity == null)
                {
                    return null;
                }

                Claim providerKeyClaim = identity.FindFirst(ClaimTypes.NameIdentifier);

                if (providerKeyClaim == null || String.IsNullOrEmpty(providerKeyClaim.Issuer) || String.IsNullOrEmpty(providerKeyClaim.Value))
                {
                    return null;
                }

                if (providerKeyClaim.Issuer == ClaimsIdentity.DefaultIssuer)
                {
                    return null;
                }

                return new ExternalLoginData
                {
                    LoginProvider = providerKeyClaim.Issuer,
                    ProviderKey = providerKeyClaim.Value,
                    UserName = identity.FindFirstValue(ClaimTypes.Name),
                    ExternalAccessToken = identity.FindFirstValue("ExternalAccessToken"),
                };
            }
        }
    }
}

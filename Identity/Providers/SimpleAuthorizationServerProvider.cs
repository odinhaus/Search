namespace Suffuz.Identity.Providers
{
    using Entities;
    using Microsoft.Owin.Security;
    using Microsoft.Owin.Security.OAuth;
    using System.Collections.Generic;
    using System.Security.Claims;
    using System.Threading.Tasks;
    using System;
    using Common.Security;
    using System.Linq;
    using Microsoft.Owin;
    using System.Net;
    using System.IO;
    using Newtonsoft.Json.Linq;

    public class SimpleAuthorizationServerProvider : OAuthAuthorizationServerProvider
    {
        private readonly AuthRepository authRepository;

        public SimpleAuthorizationServerProvider(AuthRepository authRepository)
        {
            this.authRepository = authRepository;
        }

        public override Task ValidateClientAuthentication(OAuthValidateClientAuthenticationContext context)
        {
            var clientId = default(string);
            var clientSecret = default(string);
            var client = default(Client);

            if (!context.TryGetBasicCredentials(out clientId, out clientSecret))
            {
                context.TryGetFormCredentials(out clientId, out clientSecret);
            }

            if (context.ClientId == null)
            {
                // Remove the comments from the below line context.SetError, and invalidate context 
                // if you want to force sending clientId/secrects once obtain access tokens. 
                context.Validated();

                context.SetError("invalid_clientId", "ClientId should be sent.");

                return Task.FromResult<object>(null);
            }

            client = authRepository.FindClient(context.ClientId);

            if (client == null)
            {
                context.SetError("invalid_clientId", string.Format("Client '{0}' is not registered in the system.", context.ClientId));

                return Task.FromResult<object>(null);
            }

            if (client.ApplicationType == Models.ApplicationTypes.NativeConfidential)
            {
                if (string.IsNullOrWhiteSpace(clientSecret))
                {
                    context.SetError("invalid_clientId", "Client secret should be sent.");

                    return Task.FromResult<object>(null);
                }
                else
                {
                    if (client.Secret != clientSecret.ToBase64SHA256())
                    {
                        context.SetError("invalid_clientId", "Client secret is invalid.");

                        return Task.FromResult<object>(null);
                    }
                }
            }

            if (!client.Active)
            {
                context.SetError("invalid_clientId", "Client is inactive.");

                return Task.FromResult<object>(null);
            }

            context.OwinContext.Set<string>("as:clientAllowedOrigin", client.AllowedOrigin);
            context.OwinContext.Set<string>("as:clientRefreshTokenLifeTime", client.RefreshTokenLifeTime.ToString());

            context.Validated();

            return Task.FromResult<object>(null);
        }

        public override async Task GrantResourceOwnerCredentials(OAuthGrantResourceOwnerCredentialsContext context)
        {
            var allowedOrigin = context.OwinContext.Get<string>("as:clientAllowedOrigin");

            if (allowedOrigin == null) allowedOrigin = "*";

            // TODO: Resolve issue with allowed origin
            context.OwinContext.Response.Headers.Add("Access-Control-Allow-Origin", new[] { "*" });

            var user = await authRepository.FindUser(context.UserName, context.Password);

            if (user == null)
            {
                context.SetError("invalid_grant", "The user name or password is incorrect.");
                return;
            }

            var identity = new ClaimsIdentity(context.Options.AuthenticationType);
            var customer = authRepository.FindCustomer(user.CustomerId);

            if (customer == null)
            {
                context.Rejected();
                context.SetError("customer_not_found", "The customer associated with this user does not exist.");
                return;
            }

            identity.AddClaim(new Claim(ClaimTypes.Name, context.UserName));
            identity.AddClaim(new Claim("sub", context.UserName));
            identity.AddClaim(new Claim(identity.RoleClaimType, "user"));
            identity.AddClaim(new Claim("customer_id", customer.Id));
            identity.AddClaim(new Claim("customer_name", customer.Name));
            identity.AddClaim(new Claim(ClaimTypesEx.DeviceId, Read(context.OwinContext, ClaimTypesEx.DeviceId)));
            identity.AddClaim(new Claim("provider", "suffuz", ClaimValueTypes.String, "LOCAL AUTHORITY", "LOCAL AUTHORITY"));

            var props = new AuthenticationProperties(new Dictionary<string, string>
                {
                    { 
                        "as:client_id", (context.ClientId == null) ? string.Empty : context.ClientId
                    },
                    { 
                        "userName", context.UserName
                    },
                    {
                        "customer_id", customer.Id
                    },
                    {
                        "customer_name", customer.Name
                    },
                    {
                        "device_id", Read(context.OwinContext, ClaimTypesEx.DeviceId)
                    }
                });
            AuthenticationTicket ticket = null;
            try
            {
                ticket = AssignClaims(context, identity, props);
            }
            catch
            {
                context.Rejected();
                context.SetError("resource_not_available", "The claims provider host for the configured client could not be reached.");
                return;
            }

            try
            {
                if (context.OwinContext.Request.Query.Get("verbose") == "true"
                    || context.OwinContext.Request.ReadFormAsync().Result.Get("verbose") == "true")
                {
                    AddClaims(props, identity);
                }
            }
            catch { }

            context.Validated(ticket);
        }

        private string Read(IOwinContext context, string key)
        {
            var value = context.Request.Query.FirstOrDefault(q => q.Key.Equals(key, StringComparison.InvariantCultureIgnoreCase)).Value?.First() ?? null;
            if (string.IsNullOrEmpty(value))
            {
                value = context.Request.ReadFormAsync().Result.FirstOrDefault(f => f.Key.Equals(key, StringComparison.InvariantCultureIgnoreCase)).Value?.First() ?? null;
            }
            return value;
        }

        private AuthenticationTicket AssignClaims(OAuthGrantResourceOwnerCredentialsContext context, ClaimsIdentity identity, AuthenticationProperties props)
        {
            var client = authRepository.FindClient(context.ClientId);
            if (!string.IsNullOrEmpty(client.ClaimsUri))
            {
                foreach(var claim in GetExternalClaims(client.ClaimsUri, identity, props))
                {
                    identity.AddClaim(new Claim(claim.Type, claim.Value, claim.ValueType, "LOCAL AUTHORITY", claim.OriginalIssuer, identity));
                }
            }
            return new AuthenticationTicket(identity, props);
        }

        private IEnumerable<SerializableClaim> GetExternalClaims(string claimsUri, ClaimsIdentity identity, AuthenticationProperties props)
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
                using (var sr = new StreamReader( response.GetResponseStream()))
                {
                    var body = sr.ReadToEnd();
                    if (!string.IsNullOrEmpty(body))
                    {
                        return JArray.Parse(body).ToObject<SerializableClaim[]>();
                    }
                    else
                    {
                        return new SerializableClaim[0];
                    }
                }
            }
        }

        private void AddClaims(AuthenticationProperties props, ClaimsIdentity identity)
        {

            var claims = new List<SerializableClaim>();
            foreach(var claim in identity.Claims)
            {
                claims.Add(new SerializableClaim {
                        Type = claim.Type,
                        Issuer = claim.Issuer,
                        OriginalIssuer = claim.OriginalIssuer,
                        Value = claim.Value,
                        ValueType = claim.ValueType
                    });
            }

            var serialized = Newtonsoft.Json.JsonConvert.SerializeObject(claims);

            props.Dictionary.Add("claims", serialized);
        }

        public override Task GrantRefreshToken(OAuthGrantRefreshTokenContext context)
        {
            var originalClient = context.Ticket.Properties.Dictionary["as:client_id"];
            var currentClient = context.ClientId;

            if (originalClient != currentClient)
            {
                context.SetError("invalid_clientId", "Refresh token is issued to a different clientId.");
            
                return Task.FromResult<object>(null);
            }

            // Change auth ticket for refresh token requests
            var newIdentity = new ClaimsIdentity(context.Ticket.Identity);

            newIdentity.AddClaim(new Claim("newClaim", "newValue"));

            var newTicket = new AuthenticationTicket(newIdentity, context.Ticket.Properties);

            context.Validated(newTicket);

            return Task.FromResult<object>(null);
        }

        public override Task TokenEndpoint(OAuthTokenEndpointContext context)
        {
            foreach (KeyValuePair<string, string> property in context.Properties.Dictionary)
            {
                context.AdditionalResponseParameters.Add(property.Key, property.Value);
            }

            return Task.FromResult<object>(null);
        }
    }
}
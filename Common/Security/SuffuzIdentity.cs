using Microsoft.IdentityModel.Claims;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Principal;
using System.Text;
using System.Threading.Tasks;

namespace Common.Security
{
    public class SuffuzIdentity : IClaimsIdentity
    {
        static SuffuzIdentity()
        {
            var admin = new SuffuzIdentity("Admin", "Suffuz", true);
            admin.Claims.Add(new Claim(ClaimTypes.Role, "Admin", ClaimValueTypes.String, AppContext.Name, AppContext.Name));
            admin.Claims.Add(new Claim(ClaimTypes.NameIdentifier, "Admin", ClaimValueTypes.String, AppContext.Name, AppContext.Name));
            Admin = admin;
        }

        public SuffuzIdentity(string name, string customerId, bool isAuthenticated)
        {
            Name = name;
            IsAuthenticated = isAuthenticated;
            CustomerId = customerId;
            Claims = new ClaimCollection(this);
        }

        public string AuthenticationType
        {
            get { return HttpHeaderAuthTokenBuilder.SUFFUZ_AUTH; }
        }

        public bool IsAuthenticated { get; private set; }

        public string Name { get; private set; }

        public string CustomerId { get; private set; }

        public string BearerToken { get; set; }
        public DateTime TokenExpiration { get; set; }

        public ClaimCollection Claims
        {
            get;
            private set;
        }

        public IClaimsIdentity Actor
        {
            get;
            set;
        }

        public string Label
        {
            get;
            set;
        }

        public string NameClaimType
        {
            get;
            set;
        }

        public string RoleClaimType
        {
            get;
            set;
        }

        public System.IdentityModel.Tokens.SecurityToken BootstrapToken
        {
            get;
            set;
        }

        public IClaimsIdentity Copy()
        {
            var copy = new SuffuzIdentity(this.Name, this.CustomerId, this.IsAuthenticated)
            {
                BearerToken = this.BearerToken,
                Actor = this.Actor,
                BootstrapToken = this.BootstrapToken,
                Label = this.Label,
                NameClaimType = this.NameClaimType,
                RoleClaimType = this.RoleClaimType
            };

            var claims = new ClaimCollection(copy);
            foreach(var claim in this.Claims.ToArray())
            {
                claims.Add(new Claim(claim.ClaimType, claim.Value, claim.Value, claim.Issuer, claim.OriginalIssuer));
            }
            copy.Claims = claims;
            return copy;
        }

        public static readonly SuffuzIdentity Admin;
    }
}

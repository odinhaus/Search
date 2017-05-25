using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Common.Security
{
    public class TokenResponse
    {
        public TokenResponse()
        {
            Claims = new SerializableClaim[0];
        }

        [JsonProperty("access_token")]
        public string AccessToken { get; set; }

        [JsonProperty("token_type")]
        public string TokenType { get; set; }

        [JsonProperty("expires_in")]
        public int ExpiresIn { get; set; }

        [JsonProperty("userName")]
        public string Username { get; set; }

        [JsonProperty(".issued")]
        public string IssuedAt { get; set; }

        [JsonProperty(".expires")]
        public string ExpiresAt { get; set; }

        [JsonProperty("customer_id")]
        public string CustomerId { get; set; }
        [JsonProperty("customer_name")]
        public string CustomerName { get; set; }

        [JsonProperty("claims")]
        public string ClaimsText { get; set; }

        public SerializableClaim[] Claims { get; set; }
    }
}

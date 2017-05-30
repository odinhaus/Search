using Microsoft.IdentityModel.Clients.ActiveDirectory;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Web;

namespace Suffuz.Identity.ADFS
{
    public class ADFSAuthProvider
    {
        public async Task<string> Authorize()
        {
            var authority = "https://adfs.nnhis.local/adfs"; // ADFS public URI
            var resourceUri = "https://api.nnhis.local"; // ADFS Relying Party Trust Identifier
            var clientId = "4BD0D3C2-97DF-408B-A6D4-A8FCF84030C0"; // registered client id with ADFS
            var clientReturnURI = "http://shs.auth/"; // registered return uri for client id


            var ac = new AuthenticationContext(authority, false); // no return uri is used
            var ar = await ac.AcquireTokenAsync(resourceUri, clientId, new Uri(clientReturnURI), new PlatformParameters(PromptBehavior.Auto));//, new UserIdentifier(username + "@nnhis.local", UserIdentifierType.), "u=" + username);// new UserPasswordCredential(username, password));
            return ar.CreateAuthorizationHeader();
        }
    }
}
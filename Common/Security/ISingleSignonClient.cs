using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Common.Security
{
    public interface ISingleSignonClient
    {
        /// <summary>
        /// Initializes the client and provides the host communication channel identifier as a string.
        /// </summary>
        /// <param name="channelId"></param>
        void Initialize(string channelId);
        /// <summary>
        /// Requests that the SSO host process should initiate the logout process.  If the host responds false, 
        /// logout will not proceed, and this method will return false.
        /// </summary>
        /// <param name="username"></param>
        /// <returns></returns>
        bool TryHostLogout(string username);
        /// <summary>
        /// Requests that the SSO host process should initiate the authentication process.  If the host responds false, 
        /// login will not proceed, and this method will return false.
        /// </summary>
        /// <param name="username"></param>
        /// <returns></returns>
        bool TryHostLogin(string username);
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Principal;
using System.Text;
using System.Threading.Tasks;

namespace Common.Security
{
    public interface IProvideSecurityContext
    {
        ITokenStore TokenStore { get; }
        event EventHandler AuthenticationComplete;
        string SecuritySessionId { get; }
        IPrincipal Authenticate(string username, string password);
        IPrincipal Authenticate(string token, TokenType tokenType);
        IDisposable Impersonate(string username);
        IPrincipal CurrentPrincipal { get; }
        /// <summary>
        /// Gets the unique device id for this app vendor on the current device
        /// </summary>
        string DeviceId { get; }
        /// <summary>
        /// Gets the name of the current application security context
        /// </summary>
        string AppName { get; }
        void Logoff();
        bool ValidatePasswordComplexity(string password);
        bool ChangePassword(string currentPassword, string newPassword, string confirmPassword, out string message);
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Common.Security
{
    public enum AuthenticationResult
    {
        Success,
        Failed,
        Canceled
    }
    public interface IAuthenticator
    {
        AuthenticationResult Authenticate(out string message);
        void SignOut();
    }
}

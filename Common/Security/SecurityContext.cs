using Microsoft.IdentityModel.Claims;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Principal;
using System.Text;
using System.Threading.Tasks;

namespace Common.Security
{
    public class SecurityContext
    {
        public static void Create<T>() where T : IProvideSecurityContext
        {
            var provider = AppContext.Current.Container.GetInstance<T>();
            Current = new SecurityContext(provider);
            Global = Current;
        }

        public SecurityContext Clone()
        {
            var s = new SecurityContext(Current.Provider);
            s.CurrentPrincipal = ((IClaimsPrincipal)CurrentPrincipal).Copy();
            s.ScopeId = ScopeId;
            return s;
        }

        [ThreadStatic]
        static SecurityContext _ctx;
        public static SecurityContext Current
        {
            get

            {
                if (_ctx == null)
                {
                    _ctx = Global;
                }
                return _ctx;
            }
            private set { _ctx = value; }
        }

        public static SecurityContext Global { get; set; }
        /// <summary>
        /// A user-assignable id that can be used to share additional security state/identity across application layers
        /// </summary>
        public string ScopeId { get; set; }

        public bool ValidatePasswordComplexity(string password)
        {
            return Global.Provider.ValidatePasswordComplexity(password);
        }

        public bool ChangePassword(string currentPassword, string newPassword, string confirmPassword, out string message)
        {
            return Global.ChangePassword(currentPassword, newPassword, confirmPassword, out message);
        }

        IProvideSecurityContext _provider;
        private SecurityContext(IProvideSecurityContext provider)
        {
            _provider = provider;
        }

        /// <summary>
        /// Executes the provided func asynchronously and sets the SecurityContext.Current value for the background thread executing the task
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="action"></param>
        /// <returns></returns>
        public async Task<T> ExecuteAsync<T>(Func<T> action)
        {
            var func = new Func<SecurityContext, Func<T>, T>((s, f) =>
            {
                SecurityContext.Current = s;
                return f();
            });
            var clone = this.Clone();
            return await Task.Run(() =>
            {
                var result = func(clone, action);
                return result;
            });
        }

        public async void ExecuteAsync(Action action)
        {
            var func = new Action<SecurityContext, Action>((s, f) =>
            {
                SecurityContext.Current = s;
                action();
            });
            var clone = this.Clone();
            await Task.Run(() => func(clone, action));
        }

        IPrincipal _principal;
        public IPrincipal CurrentPrincipal
        {
            get { return _principal; }
            set
            {
                _principal = value;
                System.Threading.Thread.CurrentPrincipal = value;
            }
        }

        public IProvideSecurityContext Provider { get { return _provider; } }

        public bool Authenticate(string token, TokenType tokenType)
        {
            CurrentPrincipal = _provider.Authenticate(token, tokenType);
            var ret = CurrentPrincipal != null && (CurrentPrincipal.Identity?.IsAuthenticated ?? false);
            return ret;
        }

        public bool Authenticate(string username, string password)
        {
            // we need to set this prior to authentication, so that any downstream 
            // services utilized by the provider will have access to the current user info
            // prior to completion of the authentication process
            CurrentPrincipal = _provider.Authenticate(username, password); // update to principal returned by provider
            var ret = CurrentPrincipal != null && (CurrentPrincipal.Identity?.IsAuthenticated ?? false);
            return ret;
        }

        public void Logoff()
        {
            _provider.Logoff();
            this.CurrentPrincipal = _provider.CurrentPrincipal;
        }

        public IDisposable Impersonate(string username)
        {
            return _provider.Impersonate(username);
        }

        public void AuthenticateAsync(string username, string password, Action<bool, string> authenticationCallback = null)
        {
            Task.Factory.StartNew(() => {
                try
                {
                    var success = Authenticate(username, password);
                    if (authenticationCallback != null)
                        authenticationCallback(success, success ? "Login Successful" : "Login failed for the provided username and password.");
                }
                catch (Exception ex)
                {
                    if (authenticationCallback != null)
                    {
                        if (ex is AggregateException)
                            authenticationCallback(false, ((AggregateException)ex).Flatten().InnerException.Message);
                        else
                            authenticationCallback(false, ex.Message);
                    }
                }
            });
        }

        //public byte[] Encrypt(byte[] data)
        //{
        //    return _provider.Encrypt(data);
        //}

        //public byte[] Decrypt(byte[] data)
        //{
        //    return _provider.Decrypt(data);
        //}
    }
}

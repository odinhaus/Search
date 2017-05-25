using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.NetworkInformation;
using System.Text;
using System.Threading.Tasks;
using System.Security.Principal;
using Common.Web;
using Altus.Suffūz.Scheduling;
using Microsoft.IdentityModel.Claims;

namespace Common.Security
{
    public class SecurityContextProviderClient : SecurityContextProviderBase
    {
        public SecurityContextProviderClient(IHttpTokenAuthenticator tokenAuthenticator)
            :base(tokenAuthenticator)
        {

        }


        public override string AppName
        {
            get
            {
                return "Identity";
            }
        }

        public override IPrincipal Authenticate(string username, string password)
        {
            lock (this)
            {
                CurrentPrincipal = OnAuthenticate(username, password); // we dont know the customer ID here

                if (CurrentImpersonationPrincipal != null)
                {
                    // we were impersonating, validate we can still impersonate given the new user
                    Impersonate(CurrentImpersonationPrincipal.Identity.Name);
                }

                OnAuthenticationComplete();

                if (CurrentPrincipal.Identity.IsAuthenticated)
                {
                    if (!string.IsNullOrEmpty(((SHSIdentity)CurrentPrincipal.Identity).BearerToken))
                    {
                        var bearerToken = ((SHSIdentity)CurrentPrincipal.Identity).BearerToken;
                        var expiration = ((SHSIdentity)CurrentPrincipal.Identity).TokenExpiration;
                        TokenStore.SaveToken((IClaimsIdentity)CurrentPrincipal.Identity, TokenType.LocalStore, bearerToken, expiration);
                    }

                    // we need to keep the token up to date, because it will expire
                    Scheduler.Current.Schedule(((SHSIdentity)CurrentPrincipal.Identity).TokenExpiration.Subtract(TimeSpan.FromMinutes(2)), () =>
                    {
                        return SecurityContext.Global.Authenticate(username, password);
                    });
                }
                else
                {
                    TokenStore.DeleteToken((IClaimsIdentity)CurrentPrincipal.Identity, TokenType.OAuth_SHS);
                }
                return CurrentPrincipal;
            }
        }

        protected override SHSPrincipal OnAuthenticate(string username, string password)
        {
            CurrentPrincipal = new SHSPrincipal(new SHSIdentity(username, "", false)); // forces a new login attempt
            SecurityContext.Current.ScopeId = Guid.NewGuid().ToString();
            return AppContext.Current.Container.GetInstance<IApiClient>().Authenticate(username, password);
        }

        protected override SHSPrincipal OnAuthenticate(string username, TokenType tokenType)
        {
            CurrentPrincipal = new SHSPrincipal(new SHSIdentity(username, "", false)); // forces a new login attempt
            string token;
            DateTime expiration;
            IClaimsIdentity identity;
            if (TokenStore.TryGetToken(username, TokenType.LocalStore, out token, out expiration, out identity ) && expiration >= DateTime.Now)
            {
                CurrentPrincipal = new SHSPrincipal(identity);
            }
            return (SHSPrincipal)CurrentPrincipal;   
        }

        protected override void OnDeleteCachedKey(string keyFile)
        {
            if (File.Exists(keyFile))
                File.Delete(keyFile);
        }

        protected override string[] OnGetCachedUserKeyFiles()
        {
            return Directory.GetFiles(EncryptionKeyPath);
        }

        string _deviceId = null;
        protected override string OnGetDeviceId()
        {
            if (string.IsNullOrEmpty(_deviceId))
            {


                const int MIN_MAC_ADDR_LENGTH = 12;
                string macAddress = string.Empty;
                long maxSpeed = -1;

                foreach (var nic in NetworkInterface.GetAllNetworkInterfaces())
                {
                    string tempMac = nic.GetPhysicalAddress().ToString();
                    if (nic.Speed > maxSpeed &&
                        !string.IsNullOrEmpty(tempMac) &&
                        tempMac.Length >= MIN_MAC_ADDR_LENGTH)
                    {
                        maxSpeed = nic.Speed;
                        macAddress = tempMac;
                    }
                }

                _deviceId = macAddress;
            }

            return _deviceId;
        }

        protected override ICustomKey OnGetDeviceKey(string deviceId, uint version = 0)
        {
            // TODO: integrate this with web api when available!!!!
            return new CustomKey()
            {
                Base64Key = Convert.ToBase64String(UTF8Encoding.UTF8.GetBytes("FAKEKEY")),
                Type = KeyType.CustomerPHIEncryption.ToString(),
                Algorithm = "AesCbcPkcs7",
                Description = "Symmetric encryption key for Fore Medical",
                Version = version == 0 ? 1 : version,
                Username = this.CurrentPrincipal.Identity.Name,
                CustomerId = "6c2b995b-ff27-4995-b07e-79ca1da43e59",
                Salt = CreateRandomSalt(7)
            };
        }

        protected override string OnGetEncryptionKeyPath()
        {
            string documentsPath = System.Environment.GetFolderPath(System.Environment.SpecialFolder.Personal); // Documents folder
            string libraryPath = Path.Combine(documentsPath, "..", "Library", "Keys"); // Library folder
            Directory.CreateDirectory(libraryPath);
            return libraryPath;
        }

        protected override SHSPrincipal OnImpersonate(string username)
        {
            return new SHSPrincipal(new SHSIdentity(username, "6c2b995b-ff27-4995-b07e-79ca1da43e59", false));
        }

        protected override ICustomKey OnLoadUserKey(string file)
        {
            try
            {
                var keyText = File.ReadAllText(file);
                var key = JObject.Parse(keyText).ToObject<CustomKey>();
                return key;
            }
            catch
            {
                if (File.Exists(file))
                    File.Delete(file);
                return null;
            }
        }

        protected override void OnSaveUserKey(ICustomKey userKey)
        {
            // save locally so that we don't need to hit the API 
            var keyText = JObject.FromObject(userKey).ToString();
            var fileName = GetUserKeyFileName(userKey.Version);
            var filePath = Path.Combine(EncryptionKeyPath, fileName);
            if (File.Exists(filePath))
                File.Delete(filePath);

            using (var sw = File.CreateText(filePath))
            {
                sw.Write(keyText);
            }
        }

        public override bool ChangePassword(string currentPassword, string newPassword, string confirmPassword, out string message)
        {
            var apiClient = AppContext.Current.Container.GetInstance<ApiClient>();
            message = "";
            if (ValidatePasswords(currentPassword, newPassword, confirmPassword, out message))
            {
                try
                {
                    apiClient.ChangePassword(currentPassword, newPassword, out message);
                }
                catch (Exception ex)
                {
                    message = ex.Message;
                }
            }
            return string.IsNullOrEmpty(message);
        }

        private bool ValidatePasswords(string currentPassword, string newPassword, string confirmPassword, out string message)
        {
            message = null;
            if (string.IsNullOrEmpty(currentPassword))
            {
                message = "The Current Password cannot be empty";
            }
            else if (string.IsNullOrEmpty(newPassword))
            {
                message = "The New Password cannot be empty";
            }
            else if (!newPassword.Equals(confirmPassword))
            {
                message = "The New Password and Confirm Passwords do not match";
            }
            else if (!ValidatePasswordComplexity(newPassword))
            {
                message = "The New Password does not meet the minimum complexity rules";
            }
            return message == null;
        }
    }
}

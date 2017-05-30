using Microsoft.IdentityModel.Claims;
using Common.Security.Cryptography;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Common.Security
{
    public abstract class SecurityContextProviderBase : IProvideSecurityContext
    {
        Dictionary<string, ICustomKey> _userKeys = new Dictionary<string, ICustomKey>();

        public event EventHandler AuthenticationComplete;

        public SecurityContextProviderBase(IHttpTokenAuthenticator tokenAuthenticator)
        {
            CurrentPrincipal = new SuffuzPrincipal(new SuffuzIdentity("", "", false));
            EncryptionKeyPath = OnGetEncryptionKeyPath();
            TokenAuthenticator = tokenAuthenticator;
            GetCachedUserKeys();
            SecuritySessionId = Guid.NewGuid().ToString();
        }

        public string SecuritySessionId
        {
            get;
            private set;
        }

        public ITokenStore TokenStore
        {
            get
            {
                return AppContext.Current.Container.GetInstance<ITokenStore>();
            }
        }

        /// <summary>
        /// provides token-based authentication services
        /// </summary>
        public IHttpTokenAuthenticator TokenAuthenticator { get; private set; }

        /// <summary>
        /// Implemented by derived types to get the encryption cache key path
        /// </summary>
        /// <returns></returns>
        protected abstract string OnGetEncryptionKeyPath();

        /// <summary>
        /// The current principal set after a call to Authenticate.
        /// </summary>
		public IPrincipal CurrentPrincipal
        {
            get;
            protected set;
        }

        public string DeviceId
        {
            get
            {
                return OnGetDeviceId();
            }
        }

        protected abstract string OnGetDeviceId();

        public IPrincipal CurrentImpersonationPrincipal
        {
            get { return SuffuzImpersonationPrincipal.Current; }
        }

        public abstract string AppName { get; }

        public virtual bool ValidatePasswordComplexity(string password)
        {
            var complexityRule = ConfigurationManager.AppSettings["Password_Complexity"];
            return Regex.Match(password, complexityRule).Success;
        }

        /// <summary>
        /// Authenticates the given credentials, returning an IPrincipal with an IIdentity whose IsAuthenticated property 
        /// is set to either True or False, depending on the results of the authentication, and sets the CurrentPrincipal property 
        /// to the result.
        /// </summary>
        /// <param name="username"></param>
        /// <param name="password"></param>
        /// <returns></returns>
		public virtual IPrincipal Authenticate(string username, string password)
        {
            CurrentPrincipal = new SuffuzPrincipal(new SuffuzIdentity(username, "", false));
            CurrentPrincipal = OnAuthenticate(username, password);

            if (CurrentImpersonationPrincipal != null)
            {
                // we were impersonating, validate we can still impersonate given the new user
                Impersonate(CurrentImpersonationPrincipal.Identity.Name);
            }
            OnAuthenticationComplete();
            return CurrentPrincipal;
        }


        /// <summary>
        /// Authenticates using the provided token, returning an IPrincipal with an IIdentity whose IsAuthenticated property 
        /// is set to either True or False, depending on the results of the authentication, and sets the CurrentPrincipal property 
        /// to the result.
        /// </summary>
        /// <param name="username"></param>
        /// <param name="tokenType"></param>
        /// <returns></returns>
        public virtual IPrincipal Authenticate(string username, TokenType tokenType)
        {
            CurrentPrincipal = new SuffuzPrincipal(new SuffuzIdentity(username, "", false));
            CurrentPrincipal = OnAuthenticate(username, tokenType);

            if (CurrentImpersonationPrincipal != null)
            {
                // we were impersonating, validate we can still impersonate given the new user
                Impersonate(CurrentImpersonationPrincipal.Identity.Name);
            }
            OnAuthenticationComplete();
            return CurrentPrincipal;
        }


        public virtual void Logoff()
        {
            TokenStore.DeleteToken((IClaimsIdentity)CurrentPrincipal?.Identity, TokenType.LocalStore);
            CurrentPrincipal = new SuffuzPrincipal(new SuffuzIdentity("", "", false));
            OnAuthenticationComplete();
        }


        protected virtual void OnAuthenticationComplete()
        {
            AuthenticationComplete?.Invoke(this, new EventArgs());
        }

        /// <summary>
        /// Derived types can override this method to provide custom behavior whenever the security contect needs to retrieve the latest device key 
        /// from either local cache or custom sources (e.g. an online key store)
        /// </summary>
        /// <param name="username"></param>
        /// <returns></returns>
        //protected virtual ICustomKey OnGetLatestKey(string username)
        //{
        //    return CurrentUserKey;
        //}

        /// <summary>
        /// Implemented by derived types to authenticate the given user name and password, returning an SHSPrincipal
        /// </summary>
        /// <param name="username"></param>
        /// <param name="password"></param>
        /// <returns></returns>
        protected abstract SuffuzPrincipal OnAuthenticate(string username, string password);

        protected abstract SuffuzPrincipal OnAuthenticate(string username, TokenType tokenType);

        /// <summary>
        /// validates and assigns an IPrincipal to CurrentImpersonationPrincipal if impersonation is successful
        /// </summary>
        /// <param name="username"></param>
        /// <returns></returns>
        public virtual IDisposable Impersonate(string username)
        {
            IDisposable disposable = null;
            var impPrin = OnImpersonate(username);
            if (impPrin != null)
            {
                disposable = SuffuzImpersonationPrincipal.Impersonate(impPrin);
            }
            return disposable;
        }

        /// <summary>
        /// Implemented by derived types to provide impersonation resolution, returning an SHSPrincipal for the impersonated user
        /// </summary>
        /// <param name="username"></param>
        /// <returns></returns>
        protected abstract SuffuzPrincipal OnImpersonate(string username);

        /// <summary>
        /// Gets the user key cache path
        /// </summary>
		public string EncryptionKeyPath { get; private set; }

        ICustomKey _currentUserKey = null;

        /// <summary>
        /// Gets the latest user key to be used during encryption
        /// </summary>
		//public ICustomKey CurrentUserKey
  //      {
  //          get
  //          {
  //              if (_currentUserKey == null)
  //              {
  //                  GetLatestUserKey();
  //              }
  //              return _currentUserKey;
  //          }
  //      }




        /// <summary>
        /// Gets all cached user keys
        /// </summary>
        protected void GetCachedUserKeys()
        {
            foreach (var file in OnGetCachedUserKeyFiles())
            {
                var key = LoadUserKey(file);
                if (key != null)
                {
                    _userKeys.Add(file, key);
                }
            }
        }

        /// <summary>
        /// Implemented by derived types to return a list of locally cached keys
        /// </summary>
        /// <returns></returns>
        protected abstract string[] OnGetCachedUserKeyFiles();

        /// <summary>
        /// Creates a file name key for the provided version, using the current principal identity name
        /// </summary>
        /// <param name="version"></param>
        /// <returns></returns>
        protected virtual string GetUserKeyFileName(uint version)
        {
            if (CurrentImpersonationPrincipal == null)
                return string.Format("{2}/{0}_{1}.key", CurrentPrincipal.Identity.Name, version, EncryptionKeyPath);
            else
                return string.Format("{2}/{0}_{1}.key", CurrentPrincipal.Identity.Name, version, EncryptionKeyPath);
        }

        /// <summary>
        /// Gets the latest cached user key for the current principal identity. 
        /// If no cached keys are found, or the latest key used does not match the current principal identity password, 
        /// a new key will be requested from the key provider service.
        /// </summary>
        /// <returns></returns>
		//protected virtual ICustomKey GetLatestUserKey()
  //      {
  //          lock (this)
  //          {
  //              var name = CurrentPrincipal.Identity.Name;
  //              var foundKeys = _userKeys.Keys.Where(k => Path.GetFileName(k).StartsWith(name));
  //              ICustomKey latestKey = null;
  //              uint latestVersion = 0;

  //              foreach (var fileKey in foundKeys)
  //              {
  //                  var checkKey = _userKeys[fileKey];
  //                  if (checkKey.Version > latestVersion)
  //                  {
  //                      latestKey = checkKey;
  //                      latestVersion = checkKey.Version;
  //                  }
  //              }

  //              if (latestKey == null || !CheckUserKeyPasswordIsCurrent(latestKey))
  //              {
  //                  // this user doesn't have a local key, or the password used has been changed - get one from the provider
  //                  var serverLatestKey = GetUserKey();
  //                  if (serverLatestKey != null)
  //                      latestKey = serverLatestKey;
  //              }

  //              if (latestKey != null)
  //              {
  //                  _currentUserKey = latestKey;
  //                  return latestKey;
  //              }
  //              else
  //                  return _currentUserKey;
  //          }
  //      }

        /// <summary>
        /// Gets a user key for the provided version, and Saves it to the local key cache
        /// </summary>
        /// <param name="version"></param>
        /// <returns></returns>
        //protected virtual ICustomKey GetUserKey(uint version = 0)
        //{
        //    var theKey = OnGetDeviceKey(DeviceId, version);
        //    if (theKey != null)
        //    {
        //        theKey = ToUserKey(theKey);
        //        SaveUserKey(theKey);
        //    }
        //    return theKey;
        //}

        /// <summary>
        /// Implemented by derived types to provide the customer key for the provided version. 
        /// If the version == 0, then the latest key should be provided.
        /// </summary>
        /// <param name="version"></param>
        /// <returns></returns>
        protected abstract ICustomKey OnGetDeviceKey(string deviceId, uint version = 0);

        HashSet<string> _checkedKeys = new HashSet<string>();

        /// <summary>
        /// Compares the password used to generate userKey against the current password for the current principal
        /// </summary>
        /// <param name="userKey"></param>
        /// <returns></returns>
		//protected virtual bool CheckUserKeyPasswordIsCurrent(ICustomKey userKey)
  //      {
  //          if (userKey == null)
  //              return false;
  //          var isCurrent = _checkedKeys.Contains(userKey.Base64Key);
  //          if (!isCurrent)
  //          {
  //              var passwordUsed = userKey.Description.Split(new string[] { "Password:" }, StringSplitOptions.None).LastOrDefault().Trim();
  //              isCurrent = BCrypt.CheckPassword(((SHSIdentity)CurrentPrincipal.Identity), passwordUsed);
  //              if (isCurrent) _checkedKeys.Add(userKey.Base64Key);
  //          }
  //          return isCurrent;
  //      }

        /// <summary>
        /// Converts a provided customer key to a password-encrypted key for the current principal identity
        /// </summary>
        /// <param name="customerKey"></param>
        /// <returns></returns>
		//protected virtual ICustomKey ToUserKey(ICustomKey customerKey)
  //      {
  //          //TODO: handle Algorithm property to conditionally use different encryption algorithms
  //          var password = ((SHSIdentity)CurrentPrincipal.Identity).Password;
  //          var userPasswordBytes = UTF8Encoding.UTF8.GetBytes(password + DeviceId); // make the key specific to the device
  //                                                                                   // we need to wrap this key using user's current password
  //          var customerKeyBytes = Convert.FromBase64String(customerKey.Base64Key);
  //          // create hash from user's password + DeviceId
  //          var hashedPasswordKey = CryptoKey.HashKey(userPasswordBytes);
  //          //var csp = PCLCrypto.WinRTCrypto.SymmetricKeyAlgorithmProvider.OpenAlgorithm(PCLCrypto.SymmetricAlgorithm.AesCbcPkcs7);
  //          //// create symmetric encryption key from password hash
  //          //var key = csp.CreateSymmetricKey(hashedPasswordKey);
            
  //          // generate a random vector
  //          var vector = CryptoKey.HashIV();
  //          var key = new PasswordDeriveBytes(hashedPasswordKey, customerKey.Salt).CryptDeriveKey("TripleDES", "SHA1", 192, vector);
  //          // encrypt the customer key
  //          //var cipher = PCLCrypto.WinRTCrypto.CryptographicEngine.Encrypt(key, customerKeyBytes, vector);
  //          var tdes = new TripleDESCryptoServiceProvider();
  //          var encryptor = tdes.CreateEncryptor();
  //          byte[] cipher;
  //          using (var ms = new MemoryStream())
  //          {
  //              using (var cs = new CryptoStream(ms, encryptor, CryptoStreamMode.Write))
  //              {
  //                  using (var br = new BinaryWriter(cs))
  //                  {
  //                      br.Write(customerKeyBytes);
  //                  }
  //              }
  //              cipher = ms.ToArray();
  //          }

  //          var compositeKeyBlock = new byte[1 + cipher.Length + vector.Length];
  //          compositeKeyBlock[0] = (byte)vector.Length;
  //          Buffer.BlockCopy(vector, 0, compositeKeyBlock, 1, vector.Length);
  //          Buffer.BlockCopy(cipher, 0, compositeKeyBlock, vector.Length + 1, cipher.Length);

  //          var userKey64 = Convert.ToBase64String(compositeKeyBlock);

  //          var userKey = new CustomKey()
  //          {
  //              Type = KeyType.UserPHIEncryption.ToString(),
  //              Algorithm = customerKey.Algorithm,
  //              Description = "Symmetric PHI Key for User "
  //                  + CurrentPrincipal.Identity.Name
  //                  + " using Password: "
  //                  + BCrypt.HashPassword(password, BCrypt.GenerateSalt()),
  //              Version = customerKey.Version,
  //              Base64Key = userKey64,
  //              DeviceId = customerKey.DeviceId,
  //              Username = customerKey.Username,
  //              CustomerId = customerKey.CustomerId
  //          };

  //          return userKey;
  //      }

        /// <summary>
        /// Converts and decrypts the provided userKey to the equivalent customer key.  If the current prinicpal identity 
        /// password does not match the password used to encrypt the key, a new customer will be retrieved and cached.
        /// </summary>
        /// <param name="userKey"></param>
        /// <returns></returns>
		//protected virtual ICustomKey ToDeviceKey(ICustomKey userKey)
  //      {
  //          if (!CheckUserKeyPasswordIsCurrent(userKey))
  //          {
  //              // we can't decrypt the key because the user's current password doesn't match the password
  //              // that was used to encrypt the customer key, so we need to get the key directly from the API
  //              userKey = GetUserKey(userKey.Version); // get the corresponding customer key with the provided version and save it
  //          }

  //          // get the vector and encrypted customer key from the user key
  //          var cipherKeyBytes = Convert.FromBase64String(userKey.Base64Key);
  //          var vectorLength = cipherKeyBytes[0];
  //          var vector = cipherKeyBytes.Skip(1).Take(vectorLength).ToArray();
  //          var customerKeyEncrypted = cipherKeyBytes.Skip(1 + vectorLength).Take(cipherKeyBytes.Length - vectorLength - 1).ToArray();

  //          var password = ((SHSIdentity)CurrentPrincipal.Identity).Password;
  //          // hash the password + DeviceId as was done when encrypting the key
  //          var userPasswordBytes = UTF8Encoding.UTF8.GetBytes(password + DeviceId);
  //          // create hash from user's password
  //          var hashedPasswordKey = CryptoKey.HashKey(userPasswordBytes);

  //          //var csp = PCLCrypto.WinRTCrypto.SymmetricKeyAlgorithmProvider.OpenAlgorithm(PCLCrypto.SymmetricAlgorithm.AesCbcPkcs7);
  //          //// create the symmetric key from the password hash
  //          //var key = csp.CreateSymmetricKey(hashedPasswordKey);
  //          //// decrypt the symmetric customer key
  //          //var customerKey = PCLCrypto.WinRTCrypto.CryptographicEngine.Decrypt(key, customerKeyEncrypted, vector);
  //          var key = new PasswordDeriveBytes(hashedPasswordKey, userKey.Salt).CryptDeriveKey("TripleDES", "SHA1", 192, vector);
  //          var tdes = new TripleDESCryptoServiceProvider();
  //          var encryptor = tdes.CreateDecryptor();
  //          byte[] customerKey;

  //          using (var ms = new MemoryStream(customerKeyEncrypted))
  //          {
  //              using (var cs = new CryptoStream(ms, encryptor, CryptoStreamMode.Read))
  //              {
  //                  using (var br = new BinaryReader(cs))
  //                  {
  //                      customerKey = br.ReadBytes(customerKeyEncrypted.Length);
  //                  }
  //              }
  //          }

  //          return new CustomKey()
  //          {
  //              Algorithm = userKey.Algorithm,
  //              Type = KeyType.CustomerPHIEncryption.ToString(),
  //              Description = "Customer Symmetric PHI Encryption Key",
  //              Base64Key = Convert.ToBase64String(customerKey),
  //              Version = userKey.Version,
  //              DeviceId = userKey.DeviceId,
  //              Username = userKey.Username,
  //              CustomerId = userKey.CustomerId,
  //              Salt = userKey.Salt
  //          };
  //      }

        /// <summary>
        /// Saves the provided userKey to local cache
        /// </summary>
        /// <param name="userKey"></param>
		protected virtual void SaveUserKey(ICustomKey userKey)
        {
            OnSaveUserKey(userKey);
            var keyName = GetUserKeyFileName(userKey.Version);
            if (_userKeys.ContainsKey(keyName))
                _userKeys[keyName] = userKey;
            else
                _userKeys.Add(keyName, userKey);
        }

        /// <summary>
        /// Implemented by derived types to store the provided userKey to local cache
        /// </summary>
        /// <param name="userKey"></param>
        protected abstract void OnSaveUserKey(ICustomKey userKey);

        /// <summary>
        /// Loads a cached key for the given file name
        /// </summary>
        /// <param name="file"></param>
        /// <returns></returns>
		protected virtual ICustomKey LoadUserKey(string file)
        {
            return OnLoadUserKey(file);
        }

        /// <summary>
        /// Implemented by derived types to load a cached key from cache
        /// </summary>
        /// <param name="file"></param>
        /// <returns></returns>
        protected abstract ICustomKey OnLoadUserKey(string file);

        /// <summary>
        /// Loads a a cached user key for the provided version, if it exists. 
        /// If it does not exist, the key will be retrieved from the implemented key service.
        /// </summary>
        /// <param name="version"></param>
        /// <returns></returns>
        //protected virtual ICustomKey GetUserKeyCached(uint version)
        //{
        //    ICustomKey userKey;
        //    string userKeyName = GetUserKeyFileName(version);
        //    if (!_userKeys.TryGetValue(userKeyName, out userKey))
        //    {
        //        userKey = GetUserKey(version);
        //    }
        //    return userKey;
        //}

        /// <summary>
        /// Decrypts a data blob that was previously encrypted using the Encrypt method
        /// </summary>
        /// <param name="data"></param>
        /// <returns></returns>
		//public virtual byte[] Decrypt(byte[] data)
  //      {
  //          var customerKeyVersionLength = BitConverter.ToInt32(data, 0);
  //          var customerKeyVersion = data.Skip(4).Take(customerKeyVersionLength).ToArray();
  //          string customerId;
  //          var version = GetKeyVersionUInt(customerKeyVersion, out customerId);
  //          ICustomKey userKey;

  //          userKey = GetUserKeyCached(version);

  //          if (userKey == null)
  //              throw new InvalidOperationException("A valid decryption key could not be obtained for the current user.");

  //          var customerKey = ToDeviceKey(userKey); // convert the serialized user key back to a corresponding customer key

  //          var vectorLength = data[4 + customerKeyVersionLength];
  //          var vector = data.Skip(4 + customerKeyVersionLength + 1).Take(vectorLength).ToArray();
  //          // the encrypted data to decrypt
  //          var cipher = data.Skip(4 + customerKeyVersionLength + 1 + vectorLength).Take(data.Length - 4 + 1 + vectorLength).ToArray();

  //          var csp = PCLCrypto.WinRTCrypto.SymmetricKeyAlgorithmProvider.OpenAlgorithm(PCLCrypto.SymmetricAlgorithm.AesCbcPkcs7);
  //          // create the symmetric key from the hashed customer key bytes
  //          var hashedKey = CryptoKey.HashKey(Convert.FromBase64String(customerKey.Base64Key));
  //          var key = csp.CreateSymmetricKey(hashedKey);
  //          // decrypt using the customer key and vector used to encrypt the data
  //          var clear = PCLCrypto.WinRTCrypto.CryptographicEngine.Decrypt(key, cipher, vector);
  //          return clear;
  //      }

        /// <summary>
        /// Decrypts the given Base64 encoded cipher text that was created by a previous call to Encrypt
        /// </summary>
        /// <param name="cipherText"></param>
        /// <returns></returns>
        //public virtual string Decrypt(string cipherText)
        //{
        //    var decrypted = Decrypt(Convert.FromBase64String(cipherText));
        //    return UTF8Encoding.UTF8.GetString(decrypted, 0, decrypted.Length);
        //}

        /// <summary>
        /// Encrypts a data blob using the latest customer key
        /// </summary>
        /// <param name="data"></param>
        /// <returns></returns>
		//public virtual byte[] Encrypt(byte[] data)
  //      {
  //          var userKey = CurrentUserKey;
  //          if (userKey == null)
  //              throw new InvalidOperationException("Could not establish an encryption key.");

  //          // use the customer key to encrypt the data
  //          var customerKey = ToDeviceKey(userKey);
  //          var customerKeyVersion = GetKeyVersionBytes(userKey);

  //          var csp = PCLCrypto.WinRTCrypto.SymmetricKeyAlgorithmProvider.OpenAlgorithm(PCLCrypto.SymmetricAlgorithm.AesCbcPkcs7);
  //          var hashedKey = CryptoKey.HashKey(Convert.FromBase64String(customerKey.Base64Key));
  //          var key = csp.CreateSymmetricKey(hashedKey);
  //          var vector = CryptoKey.HashIV();

  //          // encrypt the data using the symmetric key derived from the customerKey
  //          var cipher = PCLCrypto.WinRTCrypto.CryptographicEngine.Encrypt(key, data, vector);
  //          var dataBlock = new byte[4 + customerKeyVersion.Length + 1 + vector.Length + cipher.Length];

  //          // write the encryption key version length at byte 0 - 3
  //          var customerKeyVersionLength = BitConverter.GetBytes(customerKeyVersion.Length);
  //          Buffer.BlockCopy(customerKeyVersionLength, 0, dataBlock, 0, 4);
  //          Buffer.BlockCopy(customerKeyVersion, 0, dataBlock, 4, customerKeyVersion.Length);
  //          // write the vector length (1 byte)
  //          dataBlock[4 + customerKeyVersion.Length] = (byte)vector.Length;
  //          // write the vector
  //          Buffer.BlockCopy(vector, 0, dataBlock, 4 + customerKeyVersion.Length + 1, vector.Length);
  //          // write the cipher data
  //          Buffer.BlockCopy(cipher, 0, dataBlock, 4 + customerKeyVersion.Length + 1 + vector.Length, cipher.Length);

  //          return dataBlock;
  //      }

        /// <summary>
        /// Encrypts the given string and Base64 encodes the resulting cipher text byte array
        /// </summary>
        /// <param name="clearText"></param>
        /// <returns></returns>
        //public virtual string Encrypt(string clearText)
        //{
        //    var cipherText = Encrypt(UTF8Encoding.UTF8.GetBytes(clearText));
        //    return Convert.ToBase64String(cipherText, 0, cipherText.Length);
        //}

        /// <summary>
        /// Called during encryption to set the encyrpted blob's key version header.  Derived types can override 
        /// to include additional custom data to be encrypted in the blob, such as the current impersonation context, 
        /// or other contextual information that can be used to during deserialization to provide appropriate deserialization context. 
        /// When overriding this method, GetKeyVersionUInt should also be overridden to provide complementary deserialization behavior. 
        /// By default, this method looks at the CurrentImpersonationPrincipal to encode either the current user, or impersonated user 
        /// along with the associated key version to a variable length byte array.
        /// </summary>
        /// <param name="userKey"></param>
        /// <returns></returns>
        protected virtual byte[] GetKeyVersionBytes(ICustomKey userKey)
        {
            // if we impersonate, we need to include the impersonated customer id, along with the version
            // so that when we load the key to decrypt the blob, it will resolve the correct cached key version
            // by resetting the Impersonation context used when serializing the blob
            var customerId = DeviceId;
            var customerIdBytes = UTF8Encoding.UTF8.GetBytes(customerId);
            var customerIdBytesLength = BitConverter.GetBytes(customerIdBytes.Length);
            var versionBytes = BitConverter.GetBytes(userKey.Version);
            var block = new byte[4 + customerIdBytes.Length + 4];
            Buffer.BlockCopy(customerIdBytesLength, 0, block, 0, 4);
            Buffer.BlockCopy(customerIdBytes, 0, block, 4, customerIdBytes.Length);
            Buffer.BlockCopy(versionBytes, 0, block, 4 + customerIdBytes.Length, 4);
            return block;
        }

        /// <summary>
        /// Called during decryption to read the serialized blob header, and establish deserialization context.  Derived types 
        /// which override GetKeyVersionBytes should override this method to provide appropriate deserialization of the blob header. 
        /// By default, this method simply decodes the byte array containing  into an unsigned int32.
        /// </summary>
        /// <param name="keyVersionBytes"></param>
        /// <returns></returns>
        protected virtual uint GetKeyVersionUInt(byte[] keyVersionBytes, out string customerId)
        {
            var customerIdLength = BitConverter.ToInt32(keyVersionBytes, 0);
            var customerIdBytes = keyVersionBytes.Skip(4).Take(customerIdLength).ToArray();
            customerId = UTF8Encoding.UTF8.GetString(customerIdBytes, 0, customerIdBytes.Length);
            var version = BitConverter.ToUInt32(keyVersionBytes, 4 + customerIdLength);
            return version;
        }


        public void ClearKeyCache()
        {
            foreach (var file in OnGetCachedUserKeyFiles())
            {
                OnDeleteCachedKey(file);
            }
            _userKeys.Clear();
            _currentUserKey = null;
        }

        protected abstract void OnDeleteCachedKey(string keyFile);

        public static byte[] CreateRandomSalt(int length)
        {
            // Create a buffer
            byte[] randBytes;

            if (length >= 1)
            {
                randBytes = new byte[length];
            }
            else
            {
                randBytes = new byte[1];
            }

            // Create a new RNGCryptoServiceProvider.
            RNGCryptoServiceProvider rand = new RNGCryptoServiceProvider();

            // Fill the buffer with random bytes.
            rand.GetBytes(randBytes);

            // return the bytes.
            return randBytes;
        }

        public abstract bool ChangePassword(string currentPassword, string newPassword, string confirmPassword, out string message);
    }
}

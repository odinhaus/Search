using Microsoft.IdentityModel.Claims;
using Common.Extensions;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.IsolatedStorage;
using System.Linq;
using System.Runtime.Caching;
using System.Security.Policy;
using System.Security.Principal;
using System.Text;
using System.Threading.Tasks;

namespace Common.Security
{
    public class LocalStorageTokenStore : ITokenStore
    {
        private readonly ITokenProtector Protector;
        private readonly string Root;
        private static MemoryCache _cache = new MemoryCache("TokenStore");

        static LocalStorageTokenStore()
        {
            AppContext.Current.Subscribe<TokenUpdatedEventArgs>((e) =>
            {
                var store = new LocalStorageTokenStore();
                AppContext.SetEnvironmentVariable("token_encryption_key", e.EncryptionKey);
                var key = store.GetKey(e.Username, TokenType.LocalStore);
                lock (_cache)
                {
                    if (_cache.Contains(key))
                        _cache.Remove(key);
                    SecurityContext.Global.Authenticate(e.Username, TokenType.LocalStore);
                }
            });
        }

        public LocalStorageTokenStore()
        {
            this.Protector = AppContext.Current.Container.GetInstance<ITokenProtector>();
            this.Root = Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.ApplicationData), "Suffuz");
            if (!Directory.Exists(Root))
            {
                Directory.CreateDirectory(Root);
            }
        }

        public bool TryGetToken(string username, TokenType tokenType, out string token, out DateTime expiration, out IClaimsIdentity identity)
        {
            try
            {
                token = GetToken(username, tokenType, out expiration, out identity);
                return true;
            }
            catch
            {
                token = null;
                expiration = DateTime.MinValue;
                identity = null;
                return false;
            }
        }

        public string GetToken(string username, TokenType tokenType, out DateTime expiration, out IClaimsIdentity identity)
        {
            try
            {
                var key = GetKey(username, tokenType);
                lock (_cache)
                {
                    if (_cache.Contains(key))
                    {
                        identity = _cache[key] as IClaimsIdentity;
                        expiration = ((SuffuzIdentity)identity).TokenExpiration;
                        return ((SuffuzIdentity)identity).BearerToken;
                    }


                    using (var isoStream = File.Open(key, FileMode.Open, FileAccess.Read, FileShare.Read))
                    {
                        using (var cr = new BinaryReader(isoStream))
                        {
                            var cipher = cr.ReadBytes(cr.ReadInt32());
                            using (var ms = new MemoryStream(Protector.Unprotect(cipher)))
                            {
                                using (var br = new BinaryReader(ms))
                                {
                                    var user = br.ReadString();
                                    var customer = br.ReadString();
                                    var authType = br.ReadString();
                                    expiration = DateTime.FromBinary(br.ReadInt64());
                                    var claimsCount = br.ReadInt32();
                                    var claims = new List<Claim>();
                                    for (int i = 0; i < claimsCount; i++)
                                    {
                                        var claim = new Claim(br.ReadString(), br.ReadString(), br.ReadString(), br.ReadString(), br.ReadString());
                                        var propCount = br.ReadInt32();
                                        for (int j = 0; j < propCount; j++)
                                        {
                                            claim.Properties.Add(br.ReadString(), br.ReadString());
                                        }
                                        claims.Add(claim);
                                    }
                                    var token = br.ReadString();
                                    identity = new SuffuzIdentity(user, customer, true)
                                    {
                                        BearerToken = token,
                                        TokenExpiration = expiration
                                    };
                                    foreach (var claim in claims)
                                    {
                                        identity.Claims.Add(claim);
                                    }

                                    _cache.Add(key, identity, new CacheItemPolicy() { AbsoluteExpiration = expiration });
                                    return token;
                                }
                            }
                        }
                    }
                }
            }
            catch
            {
                expiration = DateTime.MinValue;
                identity = null;
                return null;
            }
        }

        public void SaveToken(IClaimsIdentity identity, TokenType tokenType, string token, DateTime expiration)
        {
            var key = GetKey(identity.Name, tokenType);

            lock (_cache)
            {
                if (_cache.Contains(key))
                {
                    _cache.Remove(key);
                }
                _cache.Add(key, identity, new CacheItemPolicy() { AbsoluteExpiration = expiration });

                if (File.Exists(key))
                    File.Delete(key);

                using (var ms = new MemoryStream())
                {
                    using (var bw = new BinaryWriter(ms))
                    {
                        bw.Write(identity.Name);
                        if (identity is SuffuzIdentity)
                            bw.Write(((SuffuzIdentity)identity).CustomerId);
                        else
                            bw.Write("");
                        bw.Write(identity.AuthenticationType);
                        bw.Write(expiration.ToBinary());
                        bw.Write(identity.Claims.Count);
                        foreach (var claim in identity.Claims)
                        {
                            bw.Write(claim.ClaimType);
                            bw.Write(claim.Value);
                            bw.Write(claim.ValueType);
                            bw.Write(claim.Issuer);
                            bw.Write(claim.OriginalIssuer);


                            bw.Write(claim.Properties.Count);
                            foreach (var prop in claim.Properties)
                            {
                                bw.Write(prop.Key);
                                bw.Write(prop.Value);
                            }
                        }

                        bw.Write(token);
                    }
                    using (var isoStream = File.Create(key))
                    {
                        using (var bw = new BinaryWriter(isoStream))
                        {
                            var cipher = Protector.Protect(ms.ToArray());
                            bw.Write(cipher.Length);
                            bw.Write(cipher);
                        }
                    }
                }

                AppContext.Current.Raise(new TokenUpdatedEventArgs(identity.Name, Protector.EncryptionKey));
            }
        }

        public void DeleteToken(IClaimsIdentity identity, TokenType tokenType)
        {
            var key = GetKey(identity.Name, tokenType);
            lock (_cache)
            {
                if (File.Exists(key))
                    File.Delete(key);
                if (_cache.Contains(key))
                    _cache.Remove(key);
            }
        }

        private string GetKey(string username, TokenType tokenType)
        {
            return Path.Combine(Root, (username + tokenType.ToString()).ToBase64SHA256().Replace("\\", "_").Replace("/","_") + ".dat");
        }
    }
}

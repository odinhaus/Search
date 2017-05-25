using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace Common.Security
{
    public class RijndaelTokenProtector : ITokenProtector
    {
        private readonly string aesKey;
        private readonly string aesSalt;

        public RijndaelTokenProtector()
        {
            EncryptionKey = AppContext.GetEnvironmentVariable<string>("token_encryption_key", Guid.NewGuid().ToString() + "|" + Guid.NewGuid().ToString());
            AppContext.EnvironmentVariableChanged += AppContext_EnvironmentVariableChanged;
            var split = EncryptionKey.Split('|');
            this.aesKey = split[0];
            this.aesSalt = split[1];
        }

        private void AppContext_EnvironmentVariableChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName.Equals("token_encryption_key"))
            {
                EncryptionKey = AppContext.GetEnvironmentVariable(e.PropertyName, EncryptionKey);
            }
        }

        public string EncryptionKey
        {
            get;
            private set;
        }


        private RijndaelManaged CreateCrypto()
        {
            var key = new Rfc2898DeriveBytes(aesKey, ASCIIEncoding.ASCII.GetBytes(aesSalt));
            var aes = new RijndaelManaged();
            aes.Key = key.GetBytes(aes.KeySize / 8);
            aes.IV = key.GetBytes(aes.BlockSize / 8);
            return aes;
        }

        public byte[] Protect(byte[] plain)
        {
            var aes = CreateCrypto();
            var encryptor = aes.CreateEncryptor();
            var ms = new MemoryStream();
            using (var cs = new CryptoStream(ms , encryptor, CryptoStreamMode.Write))
            using (var bw = new BinaryWriter(cs))
            {
                bw.Write(plain.Length);
                bw.Write(plain);
            }
            return ms.ToArray();
        }

        public byte[] Unprotect(byte[] cipher)
        {
            try
            {
                var aes = CreateCrypto();
                var decryptor = aes.CreateDecryptor();
                var ms = new MemoryStream(cipher);
                using (var cs = new CryptoStream(ms, decryptor, CryptoStreamMode.Read))
                using (var br = new BinaryReader(cs))
                {
                    var length = br.ReadInt32();
                    return br.ReadBytes(length);
                }
            }
            catch
            {
                return null;
            }
        }
    }
}

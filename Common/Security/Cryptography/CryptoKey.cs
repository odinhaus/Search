using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Common.Security.Cryptography
{
    public static class CryptoKey
    {
        public const string USER_KEY = "UserKey";

        public static byte[] EncryptKey(byte[] clearKey, string password)
        {
            byte[] vector;
            var key = EncryptKey(clearKey, password, out vector);
            return CombineKeyAndVector(key, vector);
        }

        public static byte[] EncryptKey(byte[] clearKey, string password, out byte[] vector)
        {
            var csp = PCLCrypto.WinRTCrypto.SymmetricKeyAlgorithmProvider.OpenAlgorithm(PCLCrypto.SymmetricAlgorithm.AesCbcPkcs7);
            var hashedKey = HashKey(UTF8Encoding.UTF8.GetBytes(password));
            var newkey = csp.CreateSymmetricKey(hashedKey);
            vector = HashIV();
            return PCLCrypto.WinRTCrypto.CryptographicEngine.Encrypt(newkey, clearKey, vector);
        }

        public static byte[] DecryptKey(byte[] keyAndVector, string password)
        {
            byte[] key, vector;
            SplitKeyAndVector(keyAndVector, out key, out vector);
            return key;
        }

        public static byte[] DecryptKey(byte[] cipherKey, byte[] vector, string password)
        {
            var csp = PCLCrypto.WinRTCrypto.SymmetricKeyAlgorithmProvider.OpenAlgorithm(PCLCrypto.SymmetricAlgorithm.AesCbcPkcs7);
            var hashedKey = HashKey(UTF8Encoding.UTF8.GetBytes(password));
            var newkey = csp.CreateSymmetricKey(hashedKey);
            return PCLCrypto.WinRTCrypto.CryptographicEngine.Decrypt(newkey, cipherKey, vector);
        }

        public static byte[] HashKey(byte[] key)
        {
            var sha1 = new System.Security.Cryptography.SHA1CryptoServiceProvider();
            return sha1.ComputeHash(key).Take(8).ToArray();
        }

        public static byte[] HashIV()
        {
            return HashKey(BitConverter.GetBytes(System.Environment.TickCount));
        }

        public static byte[] CombineKeyAndVector(byte[] key, byte[] vector)
        {
            var block = new byte[key.Length + vector.Length + 1];
            block[0] = (byte)vector.Length;
            Buffer.BlockCopy(vector, 0, block, 1, vector.Length);
            Buffer.BlockCopy(key, 0, block, vector.Length + 1, key.Length);
            return block;
        }

        public static void SplitKeyAndVector(byte[] keyAndVector, out byte[] key, out byte[] vector)
        {
            var vectorLength = keyAndVector[0];
            vector = keyAndVector.Skip(1).Take(vectorLength).ToArray();
            key = keyAndVector.Skip(1 + vectorLength).Take(keyAndVector.Length - 1 - vectorLength).ToArray();
        }

    }
}

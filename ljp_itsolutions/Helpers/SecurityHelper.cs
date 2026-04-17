using System.Security.Cryptography;
using System.Text;

namespace ljp_itsolutions.Helpers
{
    public static class SecurityHelper
    {
        private static readonly string EncryptionKey = "LJP_Coffee_ERP_Secure_Key_2026_!!"; 
        private static readonly byte[] Salt = new byte[] { 0x49, 0x76, 0x61, 0x6e, 0x20, 0x4d, 0x65, 0x64, 0x76, 0x65, 0x64, 0x65, 0x76 };

        public static string Encrypt(string clearText)
        {
            if (string.IsNullOrEmpty(clearText)) return clearText;

            byte[] clearBytes = Encoding.Unicode.GetBytes(clearText);
            using (Aes encryptor = Aes.Create())
            {
                encryptor.Key = Rfc2898DeriveBytes.Pbkdf2(EncryptionKey, Salt, 1000, HashAlgorithmName.SHA256, 32);
                encryptor.IV = Rfc2898DeriveBytes.Pbkdf2(EncryptionKey, Salt, 1000, HashAlgorithmName.SHA256, 16);
                using (MemoryStream ms = new MemoryStream())
                {
                    using (CryptoStream cs = new CryptoStream(ms, encryptor.CreateEncryptor(), CryptoStreamMode.Write))
                    {
                        cs.Write(clearBytes, 0, clearBytes.Length);
                        cs.Close();
                    }
                    clearText = Convert.ToBase64String(ms.ToArray());
                }
            }
            return clearText;
        }

        public static string Decrypt(string cipherText)
        {
            if (string.IsNullOrEmpty(cipherText)) return cipherText;

            try
            {
                byte[] cipherBytes = Convert.FromBase64String(cipherText);
                using (Aes encryptor = Aes.Create())
                {
                    encryptor.Key = Rfc2898DeriveBytes.Pbkdf2(EncryptionKey, Salt, 1000, HashAlgorithmName.SHA256, 32);
                    encryptor.IV = Rfc2898DeriveBytes.Pbkdf2(EncryptionKey, Salt, 1000, HashAlgorithmName.SHA256, 16);
                    using (MemoryStream ms = new MemoryStream())
                    {
                        using (CryptoStream cs = new CryptoStream(ms, encryptor.CreateDecryptor(), CryptoStreamMode.Write))
                        {
                            cs.Write(cipherBytes, 0, cipherBytes.Length);
                            cs.Close();
                        }
                        cipherText = Encoding.Unicode.GetString(ms.ToArray());
                    }
                }
                return cipherText;
            }
            catch
            {
                return cipherText;
            }
        }

        public static string MaskUsername(string? username)
        {
            if (string.IsNullOrEmpty(username)) return "N/A";
            if (username.Length <= 2) return "**";
            return username.Substring(0, 1) + new string('*', username.Length - 2) + username.Substring(username.Length - 1);
        }

        public static string MaskEmail(string? email)
        {
            if (string.IsNullOrEmpty(email)) return "N/A";
            var parts = email.Split('@');
            if (parts.Length != 2) return MaskUsername(email);
            var name = parts[0];
            var domain = parts[1];
            if (name.Length <= 2) return "**@" + domain;
            return name.Substring(0, 1) + "***" + name.Substring(name.Length - 1) + "@" + domain;
        }

        public static string MaskIpAddress(string? ip)
        {
            if (string.IsNullOrEmpty(ip) || ip == "::1" || ip == "127.0.0.1") return ip ?? "unknown";
            var parts = ip.Split('.');
            if (parts.Length != 4) return "x.x.x.x"; // Mask non-IPv4 for simplicity
            return $"{parts[0]}.{parts[1]}.x.x";
        }
    }
}

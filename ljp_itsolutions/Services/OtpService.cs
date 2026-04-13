using System.Security.Cryptography;
using System.Text;

namespace ljp_itsolutions.Services
{
    public interface IOtpService
    {
        string GenerateSecret();
        string GetTotpCode(string secret);
        bool VerifyCode(string secret, string code);
        string GetQrCodeData(string username, string secret);
    }

    public class OtpService : IOtpService
    {
        private static readonly DateTime _unixEpoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        public string GenerateSecret()
        {
            byte[] bytes = new byte[20];
            using (var rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(bytes);
            }
            return Base32Encode(bytes);
        }

        public string GetTotpCode(string secret)
        {
            long iteration = (long)(DateTime.UtcNow - _unixEpoch).TotalSeconds / 30;
            return GenerateTotp(secret, iteration);
        }

        public bool VerifyCode(string secret, string code)
        {
            if (string.IsNullOrEmpty(code)) return false;
            
            long iteration = (long)(DateTime.UtcNow - _unixEpoch).TotalSeconds / 30;
            
            // Allow 1 step window (30 seconds before and after)
            for (long i = -1; i <= 1; i++)
            {
                if (GenerateTotp(secret, iteration + i) == code)
                    return true;
            }
            
            return false;
        }

        public string GetQrCodeData(string username, string secret)
        {
            string issuer = "LJP_IT_Solutions";
            return $"otpauth://totp/{issuer}:{username}?secret={secret}&issuer={issuer}";
        }

        private string GenerateTotp(string secret, long iteration)
        {
            byte[] key = Base32Decode(secret);
            byte[] counter = BitConverter.GetBytes(iteration);
            if (BitConverter.IsLittleEndian) Array.Reverse(counter);

            using (var hmac = new HMACSHA1(key))
            {
                byte[] hash = hmac.ComputeHash(counter);
                int offset = hash[hash.Length - 1] & 0xf;
                int truncatedHash = ((hash[offset] & 0x7f) << 24) |
                                    ((hash[offset + 1] & 0xff) << 16) |
                                    ((hash[offset + 2] & 0xff) << 8) |
                                    (hash[offset + 3] & 0xff);

                int otp = truncatedHash % 1000000;
                return otp.ToString("D6");
            }
        }

        private static string Base32Encode(byte[] data)
        {
            const string alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";
            StringBuilder output = new StringBuilder();
            int i = 0;
            int index = 0;
            int digit = 0;
            while (i < data.Length)
            {
                int currentByte = data[i];
                if (index > 3)
                {
                    int nextByte = (i + 1 < data.Length) ? data[i + 1] : 0;
                    digit = currentByte & (0xFF >> index);
                    index = (index + 5) % 8;
                    digit <<= index;
                    digit |= nextByte >> (8 - index);
                    i++;
                }
                else
                {
                    digit = (currentByte >> (8 - (index + 5))) & 0x1F;
                    index = (index + 5) % 8;
                    if (index == 0) i++;
                }
                output.Append(alphabet[digit]);
            }
            return output.ToString();
        }

        private static byte[] Base32Decode(string base32)
        {
            const string alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";
            base32 = base32.ToUpper();
            List<byte> output = new List<byte>();
            int bits = 0;
            int buffer = 0;
            foreach (char c in base32)
            {
                int val = alphabet.IndexOf(c);
                if (val < 0) continue;
                buffer = (buffer << 5) | val;
                bits += 5;
                if (bits >= 8)
                {
                    output.Add((byte)(buffer >> (bits - 8)));
                    bits -= 8;
                }
            }
            return output.ToArray();
        }
    }
}

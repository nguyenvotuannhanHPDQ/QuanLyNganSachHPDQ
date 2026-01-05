using System.Security.Cryptography;
using System.Text;

namespace QuanLyNganSach.Helpers
{
    public static class SecurityHelper
    {
        public static string Sha256Hash(string raw)
        {
            using (SHA256 sha = SHA256.Create())
            {
                var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(raw));
                StringBuilder sb = new StringBuilder();
                foreach (var b in bytes)
                    sb.Append(b.ToString("X2"));
                return sb.ToString();
            }
        }
    }
}
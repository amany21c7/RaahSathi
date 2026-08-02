using System;
using System.Security.Cryptography;
using System.Text;

namespace RaahSathi.Services
{
    public static class PasswordHasher
    {
        private const string Salt = "RaahSathiSecureSalt2026!";

        public static string HashPassword(string password)
        {
            if (string.IsNullOrEmpty(password)) return string.Empty;

            using (var sha256 = SHA256.Create())
            {
                var combined = password + Salt;
                byte[] bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(combined));
                var builder = new StringBuilder();
                for (int i = 0; i < bytes.Length; i++)
                {
                    builder.Append(bytes[i].ToString("x2"));
                }
                return builder.ToString();
            }
        }

        public static bool VerifyPassword(string enteredPassword, string storedHash)
        {
            if (string.IsNullOrEmpty(enteredPassword) || string.IsNullOrEmpty(storedHash)) return false;
            string enteredHash = HashPassword(enteredPassword);
            return string.Equals(enteredHash, storedHash, StringComparison.OrdinalIgnoreCase);
        }
    }
}

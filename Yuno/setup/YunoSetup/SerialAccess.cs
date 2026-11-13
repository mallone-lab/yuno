using System;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
namespace YunoSetup
{
    public static class SerialAccess
    {
        public static bool Validate(string serial)
        {
            if (string.IsNullOrWhiteSpace(serial) || serial.Length > 128) return false;
            var normalized = serial.Trim().Replace("-", "").ToUpperInvariant();
            if (!Regex.IsMatch(normalized, @"\AYUNO[0-9A-F]{32}\z")) return false;
            var salt = Convert.FromBase64String("4Okb2OQSVoJ9JMQnyUONKw==");
            var expected = Convert.FromBase64String("h87Ukx8mzyr/RjZ6gjfl4/ze7AchiPzyjRBjfy9RSdw=");
            using (var kdf = new Rfc2898DeriveBytes(normalized, salt, 210000, HashAlgorithmName.SHA256))
            {
                var actual = kdf.GetBytes(expected.Length);
                int difference = 0;
                for (int i = 0; i < actual.Length; i++) difference |= actual[i] ^ expected[i];
                Array.Clear(actual, 0, actual.Length);
                return difference == 0;
            }
        }
        public static void Require(string serial)
        {
            if (!Validate(serial)) throw new UnauthorizedAccessException("Serial inválido. Solicite sua chave de acesso ao responsável pelo YUNO.");
        }
    }
}

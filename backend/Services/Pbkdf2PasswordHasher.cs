using System.Globalization;
using System.Security.Cryptography;

namespace Storefront.Api.Services
{
    /// <summary>
    /// PBKDF2-HMAC-SHA256 password hashing, the same primitive ASP.NET Core
    /// Identity uses by default.
    /// </summary>
    /// <remarks>
    /// The digest is stored in a self-describing format so the work factor can be
    /// raised later without invalidating existing passwords:
    /// <c>pbkdf2-sha256$iterations$base64(salt)$base64(hash)</c>.
    /// </remarks>
    public class Pbkdf2PasswordHasher : IPasswordHasher
    {
        private const string Prefix = "pbkdf2-sha256";
        private const int SaltBytes = 16;
        private const int HashBytes = 32;

        // OWASP's 2023 floor for PBKDF2-HMAC-SHA256.
        private const int DefaultIterations = 600_000;

        public string Hash(string password)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(password);

            var salt = RandomNumberGenerator.GetBytes(SaltBytes);
            var hash = Derive(password, salt, DefaultIterations);

            // Invariant culture: the iteration count is parsed back by Verify, so
            // it must not be formatted with locale-specific digits or separators.
            return string.Join('$',
                Prefix,
                DefaultIterations.ToString(CultureInfo.InvariantCulture),
                Convert.ToBase64String(salt),
                Convert.ToBase64String(hash));
        }

        public bool Verify(string password, string encodedHash)
        {
            if (string.IsNullOrWhiteSpace(password) || string.IsNullOrWhiteSpace(encodedHash))
                return false;

            var parts = encodedHash.Split('$');
            if (parts.Length != 4 || parts[0] != Prefix)
                return false;

            if (!int.TryParse(parts[1], CultureInfo.InvariantCulture, out var iterations)
                || iterations <= 0)
            {
                return false;
            }

            byte[] salt;
            byte[] expected;
            try
            {
                salt = Convert.FromBase64String(parts[2]);
                expected = Convert.FromBase64String(parts[3]);
            }
            catch (FormatException)
            {
                return false;
            }

            var actual = Derive(password, salt, iterations, expected.Length);

            return CryptographicOperations.FixedTimeEquals(actual, expected);
        }

        private static byte[] Derive(
            string password, byte[] salt, int iterations, int outputBytes = HashBytes) =>
            Rfc2898DeriveBytes.Pbkdf2(
                password, salt, iterations, HashAlgorithmName.SHA256, outputBytes);
    }
}

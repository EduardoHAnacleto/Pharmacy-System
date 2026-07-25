using PharmacyWorkerAPI.Models;

namespace PharmacyWorkerAPI.Services
{
    public interface ITokenService
    {
        /// <summary>Issues a signed access token and reports when it expires.</summary>
        (string Token, DateTime ExpiresAtUtc) CreateAccessToken(User user);

        /// <summary>
        /// Creates a refresh token. The raw value is returned to the caller once and
        /// never persisted; only the returned record's hash reaches the database.
        /// </summary>
        (string RawToken, RefreshToken Record) CreateRefreshToken(int userId);

        /// <summary>Hashes a raw refresh token for lookup or comparison.</summary>
        string HashRefreshToken(string rawToken);
    }
}

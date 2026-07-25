using Microsoft.EntityFrameworkCore;
using PharmacyWorkerAPI.Data;
using PharmacyWorkerAPI.Models;

namespace PharmacyWorkerAPI.Services
{
    /// <summary>Result of a login or refresh attempt.</summary>
    public record AuthOutcome(
        bool Succeeded,
        string? AccessToken = null,
        DateTime ExpiresAtUtc = default,
        string? RawRefreshToken = null,
        DateTime RefreshExpiresAtUtc = default,
        User? User = null)
    {
        public static AuthOutcome Failed() => new(false);
    }

    public class AuthService
    {
        /// <summary>
        /// A real digest of a value nobody knows, verified against when the username
        /// does not exist. Without it an unknown username returns before any key
        /// derivation runs, and the response time alone reveals which accounts exist.
        /// Computed once per process, not per request.
        /// </summary>
        private static readonly Lazy<string> DummyHash =
            new(() => new Pbkdf2PasswordHasher().Hash(Guid.NewGuid().ToString()));

        private readonly AppDbContext _context;
        private readonly IPasswordHasher _passwordHasher;
        private readonly ITokenService _tokenService;
        private readonly ILogger<AuthService> _logger;

        public AuthService(
            AppDbContext context,
            IPasswordHasher passwordHasher,
            ITokenService tokenService,
            ILogger<AuthService> logger)
        {
            _context = context;
            _passwordHasher = passwordHasher;
            _tokenService = tokenService;
            _logger = logger;
        }

        // ===============================
        // LOGIN
        // ===============================
        public async Task<AuthOutcome> LoginAsync(string username, string password)
        {
            var user = await _context.Users
                .FirstOrDefaultAsync(u => u.Username == username);

            var storedHash = user?.PasswordHash ?? DummyHash.Value;
            var passwordMatches = _passwordHasher.Verify(password, storedHash);

            if (user == null || !passwordMatches || !user.IsActive)
            {
                _logger.LogWarning("Failed login attempt for username {Username}.", username);
                return AuthOutcome.Failed();
            }

            user.LastLoginAt = DateTime.UtcNow;

            return await IssueTokensAsync(user);
        }

        // ===============================
        // REFRESH
        // ===============================
        public async Task<AuthOutcome> RefreshAsync(string rawRefreshToken)
        {
            if (string.IsNullOrWhiteSpace(rawRefreshToken))
                return AuthOutcome.Failed();

            var tokenHash = _tokenService.HashRefreshToken(rawRefreshToken);

            var stored = await _context.RefreshTokens
                .Include(t => t.User)
                .FirstOrDefaultAsync(t => t.TokenHash == tokenHash);

            if (stored == null || !stored.IsActive(DateTime.UtcNow) || stored.User is not { IsActive: true })
            {
                _logger.LogWarning("Refresh rejected for an unknown, expired or revoked token.");
                return AuthOutcome.Failed();
            }

            // Rotate: the presented token is spent, whether or not it was stolen.
            stored.RevokedAt = DateTime.UtcNow;

            return await IssueTokensAsync(stored.User);
        }

        // ===============================
        // LOGOUT
        // ===============================
        public async Task LogoutAsync(string? rawRefreshToken)
        {
            if (string.IsNullOrWhiteSpace(rawRefreshToken))
                return;

            var tokenHash = _tokenService.HashRefreshToken(rawRefreshToken);

            var stored = await _context.RefreshTokens
                .FirstOrDefaultAsync(t => t.TokenHash == tokenHash);

            if (stored is { RevokedAt: null })
            {
                stored.RevokedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();
            }
        }

        private async Task<AuthOutcome> IssueTokensAsync(User user)
        {
            var (accessToken, accessExpiresAt) = _tokenService.CreateAccessToken(user);
            var (rawRefreshToken, refreshRecord) = _tokenService.CreateRefreshToken(user.Id);

            _context.RefreshTokens.Add(refreshRecord);
            await _context.SaveChangesAsync();

            return new AuthOutcome(
                Succeeded: true,
                AccessToken: accessToken,
                ExpiresAtUtc: accessExpiresAt,
                RawRefreshToken: rawRefreshToken,
                RefreshExpiresAtUtc: refreshRecord.ExpiresAt,
                User: user);
        }
    }
}

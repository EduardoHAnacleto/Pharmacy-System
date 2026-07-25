using System.Globalization;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using PharmacyWorkerAPI.Models;
using PharmacyWorkerAPI.Options;

namespace PharmacyWorkerAPI.Services
{
    public class TokenService : ITokenService
    {
        private const int RefreshTokenBytes = 32;

        private readonly JwtOptions _options;
        private readonly SigningCredentials _signingCredentials;

        public TokenService(IOptions<JwtOptions> options)
        {
            _options = options.Value;

            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.SigningKey));
            _signingCredentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        }

        public (string Token, DateTime ExpiresAtUtc) CreateAccessToken(User user)
        {
            var expiresAt = DateTime.UtcNow.AddMinutes(_options.AccessTokenMinutes);

            // NameIdentifier is written as "nameid" and mapped back on validation,
            // so it also covers the subject. Emitting "sub" as well would produce a
            // duplicate NameIdentifier claim once the inbound map is applied.
            // Invariant culture: the id claim is parsed back as an int on every
            // authorized request.
            var claims = new[]
            {
                new Claim(
                    ClaimTypes.NameIdentifier,
                    user.Id.ToString(CultureInfo.InvariantCulture)),
                new Claim(ClaimTypes.Name, user.Username),
                new Claim(ClaimTypes.Role, user.Role),
            };

            var token = new JwtSecurityToken(
                issuer: _options.Issuer,
                audience: _options.Audience,
                claims: claims,
                notBefore: DateTime.UtcNow,
                expires: expiresAt,
                signingCredentials: _signingCredentials);

            return (new JwtSecurityTokenHandler().WriteToken(token), expiresAt);
        }

        public (string RawToken, RefreshToken Record) CreateRefreshToken(int userId)
        {
            // URL-safe so the value survives being carried in a cookie.
            var rawToken = Base64UrlEncoder.Encode(RandomNumberGenerator.GetBytes(RefreshTokenBytes));

            var record = new RefreshToken
            {
                UserId = userId,
                TokenHash = HashRefreshToken(rawToken),
                CreatedAt = DateTime.UtcNow,
                ExpiresAt = DateTime.UtcNow.AddDays(_options.RefreshTokenDays),
            };

            return (rawToken, record);
        }

        public string HashRefreshToken(string rawToken)
        {
            var digest = SHA256.HashData(Encoding.UTF8.GetBytes(rawToken));
            return Convert.ToHexString(digest).ToLowerInvariant();
        }
    }
}

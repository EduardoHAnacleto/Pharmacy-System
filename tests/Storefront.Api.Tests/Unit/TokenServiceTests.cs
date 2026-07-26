using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.Extensions.Options;
using Storefront.Api.Models;
using Storefront.Api.Options;
using Storefront.Api.Services;
using Xunit;

namespace Storefront.Api.Tests.Unit;

public class TokenServiceTests
{
    private static readonly JwtOptions Options = new()
    {
        Issuer = "test-issuer",
        Audience = "test-audience",
        SigningKey = "a-test-signing-key-of-at-least-32-characters",
        AccessTokenMinutes = 15,
        RefreshTokenDays = 14,
    };

    private static readonly User Admin = new()
    {
        Id = 42,
        Username = "admin",
        Role = Roles.Admin,
        PasswordHash = "irrelevant",
    };

    private static TokenService CreateService() => new(new OptionsWrapper<JwtOptions>(Options));

    [Fact]
    public void CreateAccessToken_CarriesTheIdentityAuthorizationDependsOn()
    {
        var (token, _) = CreateService().CreateAccessToken(Admin);

        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);

        Assert.Equal(Options.Issuer, jwt.Issuer);
        Assert.Contains(Options.Audience, jwt.Audiences);

        // Asserted against the ClaimTypes constants rather than wire names: the
        // token carries the long URIs, which is exactly what TokenValidationParameters
        // looks for by default, so [Authorize(Roles = ...)] and User.Identity.Name
        // resolve without any claim remapping.
        Assert.Equal("42", jwt.Claims.First(c => c.Type == ClaimTypes.NameIdentifier).Value);
        Assert.Equal("admin", jwt.Claims.First(c => c.Type == ClaimTypes.Name).Value);
        Assert.Equal(Roles.Admin, jwt.Claims.First(c => c.Type == ClaimTypes.Role).Value);
    }

    [Fact]
    public void CreateAccessToken_EmitsExactlyOneIdentifierClaim()
    {
        // Emitting both "sub" and NameIdentifier produced two NameIdentifier
        // claims once the inbound map ran; CurrentUserId reads the first.
        var (token, _) = CreateService().CreateAccessToken(Admin);

        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);

        Assert.Single(
            jwt.Claims,
            c => c.Type is "nameid" or "sub" or ClaimTypes.NameIdentifier);
    }

    [Fact]
    public void CreateAccessToken_ExpiresWithinTheConfiguredWindow()
    {
        var (_, expiresAt) = CreateService().CreateAccessToken(Admin);

        Assert.InRange(
            expiresAt,
            DateTime.UtcNow.AddMinutes(Options.AccessTokenMinutes - 1),
            DateTime.UtcNow.AddMinutes(Options.AccessTokenMinutes + 1));
    }

    [Fact]
    public void CreateRefreshToken_StoresOnlyADigest()
    {
        var (raw, record) = CreateService().CreateRefreshToken(Admin.Id);

        // A database dump must not be replayable as a set of live sessions.
        Assert.NotEqual(raw, record.TokenHash);
        Assert.DoesNotContain(raw, record.TokenHash, StringComparison.Ordinal);
        Assert.Equal(64, record.TokenHash.Length);
    }

    [Fact]
    public void CreateRefreshToken_IsUnpredictable()
    {
        var service = CreateService();

        var first = service.CreateRefreshToken(Admin.Id).RawToken;
        var second = service.CreateRefreshToken(Admin.Id).RawToken;

        Assert.NotEqual(first, second);
    }

    [Fact]
    public void HashRefreshToken_IsDeterministicSoLookupSucceeds()
    {
        var service = CreateService();
        var (raw, record) = service.CreateRefreshToken(Admin.Id);

        Assert.Equal(record.TokenHash, service.HashRefreshToken(raw));
    }

    [Fact]
    public void CreateRefreshToken_StartsActiveAndExpiresInTheFuture()
    {
        var (_, record) = CreateService().CreateRefreshToken(Admin.Id);

        Assert.True(record.IsActive(DateTime.UtcNow));
        Assert.False(record.IsActive(DateTime.UtcNow.AddDays(Options.RefreshTokenDays + 1)));
    }

    [Fact]
    public void RefreshToken_IsInactiveOnceRevoked()
    {
        var (_, record) = CreateService().CreateRefreshToken(Admin.Id);

        record.RevokedAt = DateTime.UtcNow;

        Assert.False(record.IsActive(DateTime.UtcNow));
    }
}

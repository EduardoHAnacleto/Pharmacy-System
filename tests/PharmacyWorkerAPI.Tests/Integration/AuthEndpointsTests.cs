using System.Net;
using System.Net.Http.Json;
using PharmacyWorkerAPI.DTOs.Auth;
using Xunit;

namespace PharmacyWorkerAPI.Tests.Integration;

[Collection(ApiCollection.Name)]
public class AuthEndpointsTests
{
    private readonly ApiFixture _fixture;

    public AuthEndpointsTests(ApiFixture fixture) => _fixture = fixture;

    private static object Credentials(string username, string password) =>
        new { username, password };

    [SkippableFact]
    public async Task Login_WithSeededCredentials_ReturnsAnAccessToken()
    {
        Skip.IfNot(_fixture.DockerAvailable, "Docker is not available.");
        using var client = _fixture.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/v1/auth/login",
            Credentials(ApiFixture.AdminUsername, ApiFixture.AdminPassword));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var result = await response.Content.ReadFromJsonAsync<AuthResultDto>();

        Assert.NotNull(result);
        Assert.False(string.IsNullOrWhiteSpace(result!.AccessToken));
        Assert.Equal("Admin", result.Role);
    }

    [SkippableFact]
    public async Task Login_PutsTheRefreshTokenInAnHttpOnlyCookieAndNotTheBody()
    {
        Skip.IfNot(_fixture.DockerAvailable, "Docker is not available.");
        using var client = _fixture.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/v1/auth/login",
            Credentials(ApiFixture.AdminUsername, ApiFixture.AdminPassword));

        var setCookie = Assert.Single(response.Headers.GetValues("Set-Cookie"));

        Assert.Contains("pharmacy_refresh=", setCookie, StringComparison.Ordinal);
        Assert.Contains("httponly", setCookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("samesite=strict", setCookie, StringComparison.OrdinalIgnoreCase);
        // Must match the controller route, or the browser never sends it back.
        Assert.Contains("path=/api/v1/auth", setCookie, StringComparison.OrdinalIgnoreCase);

        var body = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("refreshToken", body, StringComparison.OrdinalIgnoreCase);
    }

    [SkippableTheory]
    [InlineData("testadmin", "wrong-password")]
    [InlineData("no-such-user", "any-password")]
    public async Task Login_WithBadCredentials_Returns401WithTheSameMessage(
        string username, string password)
    {
        Skip.IfNot(_fixture.DockerAvailable, "Docker is not available.");
        using var client = _fixture.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/v1/auth/login", Credentials(username, password));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);

        // One message for every cause: distinguishing "no such user" from "wrong
        // password" hands an attacker a list of valid accounts.
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("Usuário ou senha incorretos", body, StringComparison.Ordinal);
    }

    [SkippableFact]
    public async Task Refresh_WithoutACookie_Returns401()
    {
        Skip.IfNot(_fixture.DockerAvailable, "Docker is not available.");
        using var client = _fixture.CreateClient();

        var response = await client.PostAsync("/api/v1/auth/refresh", null);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [SkippableFact]
    public async Task Me_WithoutAToken_Returns401()
    {
        Skip.IfNot(_fixture.DockerAvailable, "Docker is not available.");
        using var client = _fixture.CreateClient();

        var response = await client.GetAsync("/api/v1/auth/me");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [SkippableFact]
    public async Task Me_WithAValidToken_ReportsTheAuthenticatedAdmin()
    {
        Skip.IfNot(_fixture.DockerAvailable, "Docker is not available.");
        using var client = _fixture.CreateClient();

        var token = await LoginHelper.GetAccessTokenAsync(client);
        client.DefaultRequestHeaders.Authorization = new("Bearer", token);

        var response = await client.GetAsync("/api/v1/auth/me");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains(ApiFixture.AdminUsername, await response.Content.ReadAsStringAsync(),
            StringComparison.Ordinal);
    }

    [SkippableFact]
    public async Task Login_IsRateLimitedAfterRepeatedFailures()
    {
        Skip.IfNot(_fixture.DockerAvailable, "Docker is not available.");
        using var client = _fixture.CreateClient();

        HttpStatusCode? lastStatus = null;

        // The window permits 5; the browser-side lockout it replaced could simply
        // be cleared from localStorage.
        for (var attempt = 0; attempt < 8; attempt++)
        {
            var response = await client.PostAsJsonAsync(
                "/api/v1/auth/login", Credentials("no-such-user", "bad"));
            lastStatus = response.StatusCode;

            if (lastStatus == HttpStatusCode.TooManyRequests)
                break;
        }

        Assert.Equal(HttpStatusCode.TooManyRequests, lastStatus);
    }
}

internal static class LoginHelper
{
    public static async Task<string> GetAccessTokenAsync(HttpClient client)
    {
        var response = await client.PostAsJsonAsync(
            "/api/v1/auth/login",
            new { username = ApiFixture.AdminUsername, password = ApiFixture.AdminPassword });

        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<AuthResultDto>();

        return result!.AccessToken;
    }
}

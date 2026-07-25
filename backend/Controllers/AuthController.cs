using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using PharmacyWorkerAPI.DTOs.Auth;
using PharmacyWorkerAPI.Services;

namespace PharmacyWorkerAPI.Controllers
{
    [ApiController]
    [Route("api/v1/auth")]
    public class AuthController : ControllerBase
    {
        /// <summary>Name of the cookie carrying the refresh token.</summary>
        private const string RefreshCookieName = "pharmacy_refresh";

        /// <summary>
        /// Path the refresh cookie is scoped to. Must match the controller route:
        /// a mismatch means the browser never sends the cookie to /refresh.
        /// </summary>
        private const string RefreshCookiePath = "/api/v1/auth";

        private readonly AuthService _authService;
        private readonly IWebHostEnvironment _environment;

        public AuthController(AuthService authService, IWebHostEnvironment environment)
        {
            _authService = authService;
            _environment = environment;
        }

        // ===============================
        // LOGIN
        // ===============================
        [HttpPost("login")]
        [AllowAnonymous]
        [EnableRateLimiting("login")]
        public async Task<IActionResult> Login([FromBody] LoginRequestDto request)
        {
            var outcome = await _authService.LoginAsync(request.Username, request.Password);

            if (!outcome.Succeeded)
            {
                // One message for every failure mode. Distinguishing "no such user"
                // from "wrong password" hands an attacker a list of valid accounts.
                return Unauthorized("Usuário ou senha incorretos.");
            }

            return Ok(BuildResult(outcome));
        }

        // ===============================
        // REFRESH
        // ===============================
        [HttpPost("refresh")]
        [AllowAnonymous]
        public async Task<IActionResult> Refresh()
        {
            var rawRefreshToken = Request.Cookies[RefreshCookieName];

            var outcome = await _authService.RefreshAsync(rawRefreshToken ?? string.Empty);

            if (!outcome.Succeeded)
            {
                ClearRefreshCookie();
                return Unauthorized("Sessão expirada.");
            }

            return Ok(BuildResult(outcome));
        }

        // ===============================
        // LOGOUT
        // ===============================
        [HttpPost("logout")]
        [AllowAnonymous]
        public async Task<IActionResult> Logout()
        {
            await _authService.LogoutAsync(Request.Cookies[RefreshCookieName]);

            ClearRefreshCookie();
            return NoContent();
        }

        // ===============================
        // CURRENT USER
        // ===============================
        [HttpGet("me")]
        [Authorize]
        public IActionResult Me() => Ok(new
        {
            username = User.Identity?.Name,
            role = User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value,
        });

        // ===============================
        // COOKIE HANDLING
        // ===============================
        private AuthResultDto BuildResult(AuthOutcome outcome)
        {
            SetRefreshCookie(outcome.RawRefreshToken!, outcome.RefreshExpiresAtUtc);

            return new AuthResultDto
            {
                AccessToken = outcome.AccessToken!,
                ExpiresAtUtc = outcome.ExpiresAtUtc,
                Username = outcome.User!.Username,
                Role = outcome.User.Role,
            };
        }

        private void SetRefreshCookie(string rawToken, DateTime expiresAtUtc)
        {
            Response.Cookies.Append(RefreshCookieName, rawToken, BuildCookieOptions(expiresAtUtc));
        }

        private void ClearRefreshCookie()
        {
            // Same attributes as when it was set, otherwise the browser keeps the
            // original cookie and "logout" leaves a usable session behind.
            Response.Cookies.Append(
                RefreshCookieName, string.Empty, BuildCookieOptions(DateTime.UnixEpoch));
        }

        private CookieOptions BuildCookieOptions(DateTime expiresAtUtc) => new()
        {
            HttpOnly = true,

            // Secure would make the cookie undeliverable over plain HTTP, which is
            // how the app is served locally. Production is expected to terminate
            // TLS at the reverse proxy.
            Secure = !_environment.IsDevelopment(),

            SameSite = SameSiteMode.Strict,

            // Scoped to the auth endpoints: no other request needs to carry it.
            Path = RefreshCookiePath,

            Expires = new DateTimeOffset(expiresAtUtc, TimeSpan.Zero),
        };
    }
}

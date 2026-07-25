namespace PharmacyWorkerAPI.DTOs.Auth
{
    /// <summary>
    /// What the browser is allowed to see after a successful login.
    /// </summary>
    /// <remarks>
    /// The refresh token is deliberately absent: it travels in an HttpOnly cookie
    /// so JavaScript — and therefore any injected script — cannot read it.
    /// </remarks>
    public class AuthResultDto
    {
        public string AccessToken { get; set; } = string.Empty;

        public DateTime ExpiresAtUtc { get; set; }

        public string Username { get; set; } = string.Empty;

        public string Role { get; set; } = string.Empty;
    }
}

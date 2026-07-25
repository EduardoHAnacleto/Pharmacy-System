namespace PharmacyWorkerAPI.Options
{
    public class JwtOptions
    {
        public const string SectionName = "Jwt";

        public string Issuer { get; set; } = "pharmacy-system";
        public string Audience { get; set; } = "pharmacy-system";

        /// <summary>
        /// HMAC signing key. Supplied by the environment only — a key committed to
        /// the repository would let anyone mint valid admin tokens.
        /// </summary>
        public string SigningKey { get; set; } = string.Empty;

        /// <summary>
        /// Access tokens are deliberately short-lived: they cannot be revoked
        /// server-side, so expiry is the only thing that stops a leaked one.
        /// </summary>
        public int AccessTokenMinutes { get; set; } = 15;

        public int RefreshTokenDays { get; set; } = 14;
    }
}

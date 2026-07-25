namespace PharmacyWorkerAPI.Options
{
    /// <summary>
    /// Login throttling, configurable so an operator can tune it without a rebuild.
    /// </summary>
    /// <remarks>
    /// Also what makes the limit testable: the integration suite raises it for
    /// tests that merely need to log in, and lowers it for the one test that
    /// asserts a 429 — otherwise that single test exhausts the window and every
    /// later login in the run fails for the wrong reason.
    /// </remarks>
    public class RateLimitOptions
    {
        public const string SectionName = "RateLimit";

        /// <summary>Failed or successful login attempts permitted per window, per IP.</summary>
        public int LoginPermitLimit { get; set; } = 5;

        public int LoginWindowSeconds { get; set; } = 300;
    }
}

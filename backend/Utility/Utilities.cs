using Microsoft.Extensions.Logging;

namespace PharmacyWorkerAPI.Utility
{
    public static class Utilities
    {
        /// <summary>
        /// Resolves an IANA or Windows time zone id, falling back to UTC when the
        /// value is missing or unknown. The id arrives from a query string, so an
        /// unknown value is expected input rather than an error — it is logged at
        /// debug level and never propagated.
        /// </summary>
        public static TimeZoneInfo GetTimeZone(string? timeZone, ILogger? logger = null)
        {
            if (string.IsNullOrWhiteSpace(timeZone))
                return TimeZoneInfo.Utc;

            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById(timeZone);
            }
            catch (Exception ex) when (ex is TimeZoneNotFoundException or InvalidTimeZoneException)
            {
                logger?.LogDebug(
                    ex, "Unknown time zone {TimeZone} requested; falling back to UTC.", timeZone);
                return TimeZoneInfo.Utc;
            }
        }
    }
}

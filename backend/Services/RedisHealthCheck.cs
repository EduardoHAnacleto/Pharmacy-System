using Microsoft.Extensions.Diagnostics.HealthChecks;
using StackExchange.Redis;

namespace PharmacyWorkerAPI.Services
{
    /// <summary>
    /// Reports Redis reachability.
    /// </summary>
    /// <remarks>
    /// Degraded rather than Unhealthy when Redis is down: the cache is an
    /// optimisation, and the API answers every request without it. Reporting
    /// Unhealthy would take the container out of rotation over a non-fatal fault.
    /// </remarks>
    public class RedisHealthCheck : IHealthCheck
    {
        private readonly IConnectionMultiplexer _redis;

        public RedisHealthCheck(IConnectionMultiplexer redis) => _redis = redis;

        public async Task<HealthCheckResult> CheckHealthAsync(
            HealthCheckContext context, CancellationToken cancellationToken = default)
        {
            try
            {
                await _redis.GetDatabase().PingAsync();
                return HealthCheckResult.Healthy();
            }
            catch (Exception ex)
            {
                return HealthCheckResult.Degraded("Redis unreachable; serving without cache.", ex);
            }
        }
    }
}

using System.Text.Json;
using StackExchange.Redis;

namespace Storefront.Api.Services;

/// <summary>
/// Cache access with scope-versioned keys.
/// </summary>
/// <remarks>
/// Invalidation increments a per-scope counter that forms part of every key, so
/// old entries become unreachable and expire on their own TTL. The previous
/// implementation swept the keyspace with <c>KEYS scope*</c> on every create and
/// delete, which costs O(number of keys in Redis) and blocks the server while it
/// runs — a well-known way to stall a shared Redis under load.
/// </remarks>
public class RedisService
{
    private readonly IDatabase _db;
    private readonly ILogger<RedisService> _logger;

    public RedisService(IConnectionMultiplexer redis, ILogger<RedisService> logger)
    {
        _db = redis.GetDatabase();
        _logger = logger;
    }

    // ===============================
    // SET GENERIC
    // ===============================
    public async Task SetAsync<T>(string key, T value, TimeSpan? expiry = null)
    {
        var json = JsonSerializer.Serialize(value);
        await _db.StringSetAsync(key, json, expiry);
    }

    // ===============================
    // GET GENERIC
    // ===============================
    public async Task<T?> GetAsync<T>(string key)
    {
        var value = await _db.StringGetAsync(key);

        if (!value.HasValue)
            return default;

        return JsonSerializer.Deserialize<T>(value!);
    }

    // ===============================
    // REMOVE
    // ===============================
    public async Task RemoveAsync(string key) => await _db.KeyDeleteAsync(key);

    // ===============================
    // READ-THROUGH
    // ===============================

    /// <summary>
    /// Returns the cached value for <paramref name="key"/> inside
    /// <paramref name="scope"/>, invoking <paramref name="factory"/> on a miss.
    /// </summary>
    /// <remarks>
    /// A Redis outage must not take the API down with it, so a cache failure is
    /// logged and the factory result is returned uncached.
    /// </remarks>
    public async Task<T> GetOrSetAsync<T>(
        string scope, string key, TimeSpan ttl, Func<Task<T>> factory)
    {
        string? versionedKey = null;

        try
        {
            var version = await GetVersionAsync(scope);
            versionedKey = $"{scope}:v{version}:{key}";

            var cached = await GetAsync<T>(versionedKey);
            if (cached != null)
                return cached;
        }
        catch (RedisException ex)
        {
            _logger.LogWarning(ex, "Cache read failed for scope {Scope}; querying directly.", scope);
        }

        var value = await factory();

        if (versionedKey != null)
        {
            try
            {
                await SetAsync(versionedKey, value, ttl);
            }
            catch (RedisException ex)
            {
                _logger.LogWarning(ex, "Cache write failed for key {Key}.", versionedKey);
            }
        }

        return value;
    }

    /// <summary>
    /// Makes every cached entry in <paramref name="scope"/> unreachable, in O(1).
    /// </summary>
    public async Task InvalidateScopeAsync(string scope)
    {
        try
        {
            await _db.StringIncrementAsync(VersionKey(scope));
        }
        catch (RedisException ex)
        {
            // A failed bump means stale reads until the TTL expires, which beats
            // failing the write the caller has already committed.
            _logger.LogWarning(ex, "Failed to invalidate cache scope {Scope}.", scope);
        }
    }

    private async Task<long> GetVersionAsync(string scope)
    {
        var value = await _db.StringGetAsync(VersionKey(scope));
        return value.HasValue && value.TryParse(out long version) ? version : 0;
    }

    private static string VersionKey(string scope) => $"{scope}:version";
}

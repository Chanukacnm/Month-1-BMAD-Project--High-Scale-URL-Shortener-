using System.Collections.Concurrent;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;
using UrlShortener.Application.Common.Interfaces;

namespace UrlShortener.Infrastructure.Services;

/// <summary>
/// Redis-backed sliding window rate limiter. Falls back to an in-memory
/// ConcurrentDictionary when Redis is unavailable.
/// </summary>
public class RedisRateLimitService : IRateLimitService
{
    private readonly IDatabase? _database;
    private readonly ILogger<RedisRateLimitService> _logger;
    private readonly Dictionary<string, (int PermitLimit, int WindowSeconds)> _policies;

    // In-memory fallback
    private static readonly ConcurrentDictionary<string, (int Count, DateTime WindowStart)> _fallbackStore = new();

    public RedisRateLimitService(
        IServiceProvider serviceProvider,
        IConfiguration configuration,
        ILogger<RedisRateLimitService> logger)
    {
        _database = serviceProvider.GetService<IConnectionMultiplexer>()?.GetDatabase();
        _logger = logger;

        _policies = new Dictionary<string, (int, int)>(StringComparer.OrdinalIgnoreCase);
        var policiesSection = configuration.GetSection("RateLimiting:Policies");
        foreach (var policySection in policiesSection.GetChildren())
        {
            var permitLimit = policySection.GetValue<int>("PermitLimit", 200);
            var windowSeconds = policySection.GetValue<int>("WindowSeconds", 60);
            _policies[policySection.Key] = (permitLimit, windowSeconds);
        }

        // Default policy if none configured
        if (!_policies.ContainsKey("default"))
        {
            _policies["default"] = (200, 60);
        }
    }

    public async Task<bool> IsAllowedAsync(string clientId, string policy, CancellationToken cancellationToken = default)
    {
        var info = await GetInfoAsync(clientId, policy, cancellationToken);
        return info.IsAllowed;
    }

    public async Task<RateLimitInfo> GetInfoAsync(string clientId, string policy, CancellationToken cancellationToken = default)
    {
        var (permitLimit, windowSeconds) = _policies.TryGetValue(policy, out var p) ? p : _policies["default"];
        var key = $"ratelimit:{policy}:{clientId}";

        if (_database != null)
        {
            return await GetInfoFromRedisAsync(key, permitLimit, windowSeconds);
        }

        return GetInfoFromMemory(key, permitLimit, windowSeconds);
    }

    private async Task<RateLimitInfo> GetInfoFromRedisAsync(string key, int permitLimit, int windowSeconds)
    {
        try
        {
            var count = await _database!.StringIncrementAsync(key);

            if (count == 1)
            {
                // First request in the window — set expiry
                await _database.KeyExpireAsync(key, TimeSpan.FromSeconds(windowSeconds));
            }

            var ttl = await _database.KeyTimeToLiveAsync(key);
            var retryAfter = ttl.HasValue ? (int)Math.Ceiling(ttl.Value.TotalSeconds) : windowSeconds;
            var remaining = Math.Max(0, permitLimit - (int)count);

            return new RateLimitInfo(
                IsAllowed: count <= permitLimit,
                Limit: permitLimit,
                Remaining: remaining,
                RetryAfterSeconds: count > permitLimit ? retryAfter : 0);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Redis rate limit check failed, falling back to in-memory.");
            return GetInfoFromMemory(key, permitLimit, windowSeconds);
        }
    }

    private static RateLimitInfo GetInfoFromMemory(string key, int permitLimit, int windowSeconds)
    {
        var now = DateTime.UtcNow;

        var entry = _fallbackStore.AddOrUpdate(
            key,
            _ => (1, now),
            (_, existing) =>
            {
                if ((now - existing.WindowStart).TotalSeconds >= windowSeconds)
                {
                    return (1, now);
                }
                return (existing.Count + 1, existing.WindowStart);
            });

        var remaining = Math.Max(0, permitLimit - entry.Count);
        var retryAfter = entry.Count > permitLimit
            ? (int)Math.Ceiling(windowSeconds - (now - entry.WindowStart).TotalSeconds)
            : 0;

        return new RateLimitInfo(
            IsAllowed: entry.Count <= permitLimit,
            Limit: permitLimit,
            Remaining: remaining,
            RetryAfterSeconds: Math.Max(0, retryAfter));
    }
}

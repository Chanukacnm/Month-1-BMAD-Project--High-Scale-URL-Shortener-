using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;
using UrlShortener.Application.Common.Interfaces;
using UrlShortener.Application.Common.Models;
using UrlShortener.Domain.Entities;

namespace UrlShortener.Infrastructure.Services;

public class AnalyticsService : IAnalyticsService
{
    private readonly IDatabase? _database;
    private readonly IShardConnectionFactory _contextFactory;
    private readonly IShardRouter _router;
    private readonly ILogger<AnalyticsService> _logger;

    public AnalyticsService(
        IServiceProvider serviceProvider,
        IShardConnectionFactory contextFactory,
        IShardRouter router,
        ILogger<AnalyticsService> logger)
    {
        _database = serviceProvider.GetService<IConnectionMultiplexer>()?.GetDatabase();
        _contextFactory = contextFactory;
        _router = router;
        _logger = logger;
    }

    public async Task TrackClickAsync(ClickMetadata metadata, CancellationToken cancellationToken = default)
    {
        // 1. Atomic increment in Redis (Sync)
        if (_database != null)
        {
            try
            {
                var key = $"clicks:{metadata.ShortCode}";
                await _database.StringIncrementAsync(key);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to increment Redis click counter for {ShortCode}", metadata.ShortCode);
            }
        }

        // 2. Asynchronous detailed persistence to PostgreSQL (Fire and Forget with error handling)
        _ = Task.Run(async () =>
        {
            try
            {
                var shardIndex = _router.GetShardIndex(metadata.ShortCode);
                using var context = _contextFactory.CreateDbContext(shardIndex);

                var clickEvent = new ClickEvent
                {
                    Id = Guid.NewGuid(),
                    ShortCode = metadata.ShortCode,
                    IpAddress = metadata.IpAddress,
                    UserAgent = metadata.UserAgent,
                    Referer = metadata.Referer,
                    OccurredAt = DateTime.UtcNow
                };

                context.ClickEvents.Add(clickEvent);
                await context.SaveChangesAsync(CancellationToken.None);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to persist click event for {ShortCode}", metadata.ShortCode);
            }
        }, CancellationToken.None);
    }

    public async Task<long> GetClickCountAsync(string shortCode, CancellationToken cancellationToken = default)
    {
        // 1. Try Redis counter first (fast path)
        if (_database != null)
        {
            try
            {
                var key = $"clicks:{shortCode}";
                var redisValue = await _database.StringGetAsync(key);
                if (redisValue.HasValue && long.TryParse(redisValue, out var count))
                {
                    return count;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to read Redis click counter for {ShortCode}", shortCode);
            }
        }

        // 2. Fallback to DB count
        try
        {
            var shardIndex = _router.GetShardIndex(shortCode);
            using var context = _contextFactory.CreateDbContext(shardIndex);
            return await context.ClickEvents
                .AsNoTracking()
                .CountAsync(c => c.ShortCode == shortCode, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to count click events from DB for {ShortCode}", shortCode);
            return 0;
        }
    }
}

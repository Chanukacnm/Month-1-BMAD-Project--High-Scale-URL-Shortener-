using Microsoft.Extensions.DependencyInjection;
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

    public AnalyticsService(IConnectionMultiplexer? redis, IShardConnectionFactory contextFactory, IShardRouter router)
    {
        _database = redis?.GetDatabase();
        _contextFactory = contextFactory;
        _router = router;
    }

    public async Task TrackClickAsync(ClickMetadata metadata, CancellationToken cancellationToken = default)
    {
        // 1. Atomic increment in Redis (Sync)
        if (_database != null)
        {
            var key = $"clicks:{metadata.ShortCode}";
            await _database.StringIncrementAsync(key);
        }

        // 2. Asynchronous detailed persistence to PostgreSQL (Fire and Forget)
        _ = Task.Run(async () =>
        {
            var shardIndex = _router.GetShardIndex(metadata.ShortCode);
            var context = _contextFactory.CreateDbContext(shardIndex);

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
        }, CancellationToken.None);
    }
}

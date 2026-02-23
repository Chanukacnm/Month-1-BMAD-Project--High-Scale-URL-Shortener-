using StackExchange.Redis;
using UrlShortener.Application.Common.Interfaces;

namespace UrlShortener.Infrastructure.Services;

public class RedisCacheService : ICacheService
{
    private readonly IDatabase? _database;

    public RedisCacheService(IConnectionMultiplexer? redis = null)
    {
        _database = redis?.GetDatabase();
    }

    public async Task<string?> GetAsync(string key, CancellationToken cancellationToken = default)
    {
        if (_database == null) return null;
        try
        {
            return await _database.StringGetAsync(key);
        }
        catch (Exception)
        {
            return null;
        }
    }

    public async Task SetAsync(string key, string value, TimeSpan? expiration = null, CancellationToken cancellationToken = default)
    {
        if (_database == null) return;
        try
        {
            if (expiration.HasValue)
            {
                await _database.StringSetAsync(key, value, expiration.Value);
            }
            else
            {
                await _database.StringSetAsync(key, value);
            }
        }
        catch (Exception)
        {
        }
    }

    public async Task RemoveAsync(string key, CancellationToken cancellationToken = default)
    {
        if (_database == null) return;
        try
        {
            await _database.KeyDeleteAsync(key);
        }
        catch (Exception)
        {
        }
    }
}

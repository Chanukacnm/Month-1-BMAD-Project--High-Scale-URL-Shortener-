using UrlShortener.Application.Common.Interfaces;

namespace UrlShortener.Infrastructure.Persistence;

public class ShardRouter : IShardRouter
{
    private readonly int _shardCount;

    public ShardRouter(IShardConnectionFactory connectionFactory)
    {
        _shardCount = connectionFactory.ShardCount;
    }

    public int GetShardIndex(string shortCode)
    {
        if (string.IsNullOrEmpty(shortCode))
        {
            return 0;
        }

        // Use absolute value of hash code to stay within bounds
        var hash = Math.Abs(shortCode.GetHashCode());
        return hash % _shardCount;
    }
}

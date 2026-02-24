using System.Security.Cryptography;
using System.Text;
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

        // Deterministic hash using SHA256 — safe across process restarts and platforms.
        // string.GetHashCode() is randomized per-process in .NET Core and must NOT be
        // used for persistent routing decisions.
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(shortCode));
        // Use bitwise AND to clear sign bit instead of Math.Abs() which throws
        // OverflowException when BitConverter.ToInt32 returns int.MinValue.
        var hash = BitConverter.ToInt32(bytes, 0) & 0x7FFFFFFF;
        return hash % _shardCount;
    }
}

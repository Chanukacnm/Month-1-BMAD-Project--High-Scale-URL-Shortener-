namespace UrlShortener.Application.Common.Interfaces;

public interface IShardConnectionFactory
{
    IApplicationDbContext CreateDbContext(int shardIndex);
    int ShardCount { get; }
}

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using System.Collections.Generic;
using System.Linq;
using UrlShortener.Application.Common.Interfaces;

namespace UrlShortener.Infrastructure.Persistence;

public class ShardConnectionFactory : IShardConnectionFactory
{
    private readonly IConfiguration _configuration;
    private readonly DbContextOptions<ApplicationDbContext>[] _options;

    public ShardConnectionFactory(IConfiguration configuration)
    {
        _configuration = configuration;
        var optionsList = new List<DbContextOptions<ApplicationDbContext>>();
        
        int i = 1;
        while (true)
        {
            var conn = _configuration.GetConnectionString($"Shard{i}");
            if (string.IsNullOrEmpty(conn)) break;

            var builder = new DbContextOptionsBuilder<ApplicationDbContext>();
            if (conn.Contains("postgres-shard") || conn.Contains("localhost:543"))
            {
                // Fallback to SQLite if Docker-only connection string is found or if we're in standalone
                builder.UseSqlite($"Data Source=shard_{i}.db");
            }
            else
            {
                builder.UseNpgsql(conn);
            }
            
            optionsList.Add(builder.Options);
            i++;
        }

        // Default to 2 SQLite shards if none found
        if (optionsList.Count == 0)
        {
            for (int j = 1; j <= 2; j++)
            {
                var builder = new DbContextOptionsBuilder<ApplicationDbContext>();
                builder.UseSqlite($"Data Source=shard_{j}.db");
                optionsList.Add(builder.Options);
            }
        }

        _options = optionsList.ToArray();
    }

    public int ShardCount => _options.Length;

    public IApplicationDbContext CreateDbContext(int shardIndex)
    {
        if (shardIndex < 0 || shardIndex >= _options.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(shardIndex), "Invalid shard index.");
        }

        return new ApplicationDbContext(_options[shardIndex]);
    }
}

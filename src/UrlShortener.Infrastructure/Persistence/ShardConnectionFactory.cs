using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
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
            
            if (IsPostgresReachable(conn))
            {
                builder.UseNpgsql(conn);
            }
            else
            {
                // Fallback to SQLite when PostgreSQL is not reachable (standalone mode)
                builder.UseSqlite($"Data Source=shard_{i}.db");
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

    /// <summary>
    /// Attempts a quick TCP connection to the PostgreSQL host to check if it's reachable.
    /// Returns false if the connection fails, indicating standalone/offline mode.
    /// </summary>
    private static bool IsPostgresReachable(string connectionString)
    {
        try
        {
            // Parse host and port from connection string
            var host = "localhost";
            var port = 5432;

            var parts = connectionString.Split(';', StringSplitOptions.RemoveEmptyEntries);
            foreach (var part in parts)
            {
                var kv = part.Split('=', 2);
                if (kv.Length != 2) continue;
                var key = kv[0].Trim().ToLowerInvariant();
                var value = kv[1].Trim();

                if (key == "host" || key == "server")
                {
                    host = value;
                }
                else if (key == "port")
                {
                    int.TryParse(value, out port);
                }
            }

            using var client = new TcpClient();
            var result = client.BeginConnect(host, port, null, null);
            var success = result.AsyncWaitHandle.WaitOne(TimeSpan.FromSeconds(1));
            
            if (success && client.Connected)
            {
                client.EndConnect(result);
                return true;
            }
            
            return false;
        }
        catch
        {
            return false;
        }
    }
}

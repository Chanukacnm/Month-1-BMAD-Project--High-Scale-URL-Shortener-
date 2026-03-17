using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Configuration;
using UrlShortener.Application.Common.Interfaces;

namespace UrlShortener.Infrastructure.Persistence;

/// <summary>
/// Routes short codes to database shards based on geographic region.
/// Each region owns a contiguous range of shard indices. Within a region,
/// SHA256 hash-based modulo routing is used for deterministic shard selection.
/// </summary>
public class GeoShardRouter : IGeoShardRouter
{
    private readonly Dictionary<string, (int StartIndex, int ShardCount)> _regionMap;
    private readonly string _defaultRegion;

    public GeoShardRouter(IConfiguration configuration)
    {
        _regionMap = new Dictionary<string, (int, int)>(StringComparer.OrdinalIgnoreCase);

        var geoSection = configuration.GetSection("GeoSharding:Regions");
        var children = geoSection.GetChildren().ToList();

        if (children.Count == 0)
        {
            // Fallback: single default region with 2 shards (backward compatible)
            _regionMap["default"] = (0, 2);
            _defaultRegion = "default";
            return;
        }

        int currentIndex = 0;
        foreach (var regionSection in children)
        {
            var regionName = regionSection.Key.ToLowerInvariant();
            var shardCount = regionSection.GetValue<int>("ShardCount", 1);

            _regionMap[regionName] = (currentIndex, shardCount);
            currentIndex += shardCount;
        }

        _defaultRegion = configuration.GetValue<string>("GeoSharding:DefaultRegion")?.ToLowerInvariant()
                         ?? _regionMap.Keys.First();
    }

    public string DefaultRegion => _defaultRegion;

    public int GetShardIndex(string shortCode, string region)
    {
        var normalizedRegion = region?.ToLowerInvariant() ?? _defaultRegion;

        if (!_regionMap.TryGetValue(normalizedRegion, out var range))
        {
            range = _regionMap[_defaultRegion];
        }

        if (string.IsNullOrEmpty(shortCode))
        {
            return range.StartIndex;
        }

        // Deterministic SHA256 hash within the region's shard range
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(shortCode));
        var hash = BitConverter.ToInt32(bytes, 0) & 0x7FFFFFFF;
        return range.StartIndex + (hash % range.ShardCount);
    }

    public string GetRegionFromHeader(string? geoRegionHeader)
    {
        if (string.IsNullOrWhiteSpace(geoRegionHeader))
        {
            return _defaultRegion;
        }

        var normalized = geoRegionHeader.Trim().ToLowerInvariant();
        return _regionMap.ContainsKey(normalized) ? normalized : _defaultRegion;
    }

    public IReadOnlyList<string> GetRegions()
    {
        return _regionMap.Keys.ToList().AsReadOnly();
    }
}

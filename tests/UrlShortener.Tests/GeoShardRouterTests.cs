using Microsoft.Extensions.Configuration;
using UrlShortener.Infrastructure.Persistence;

namespace UrlShortener.Tests;

public class GeoShardRouterTests
{
    private static GeoShardRouter CreateRouter(Dictionary<string, string?> configValues)
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(configValues)
            .Build();
        return new GeoShardRouter(config);
    }

    private static Dictionary<string, string?> TwoRegionConfig() => new()
    {
        ["GeoSharding:DefaultRegion"] = "us",
        ["GeoSharding:Regions:us:ShardCount"] = "2",
        ["GeoSharding:Regions:eu:ShardCount"] = "2",
    };

    [Fact]
    public void GetShardIndex_IsDeterministic_WithinRegion()
    {
        var router = CreateRouter(TwoRegionConfig());
        var code = "testCode123";

        var first = router.GetShardIndex(code, "us");
        var second = router.GetShardIndex(code, "us");
        var third = router.GetShardIndex(code, "us");

        Assert.Equal(first, second);
        Assert.Equal(second, third);
    }

    [Fact]
    public void GetShardIndex_ReturnsConsistentRange_ForEachRegion()
    {
        var router = CreateRouter(TwoRegionConfig());

        // Each region has 2 shards. The exact start index depends on config
        // iteration order, but all US codes should map to the same 2 indices.
        var usIndices = new HashSet<int>();
        var euIndices = new HashSet<int>();

        for (int i = 0; i < 200; i++)
        {
            usIndices.Add(router.GetShardIndex($"code_{i}", "us"));
            euIndices.Add(router.GetShardIndex($"code_{i}", "eu"));
        }

        // Each region should use exactly 2 distinct shard indices
        Assert.Equal(2, usIndices.Count);
        Assert.Equal(2, euIndices.Count);

        // US and EU ranges should NOT overlap (they're contiguous but separate)
        Assert.Empty(usIndices.Intersect(euIndices));

        // All indices should be in the total range [0, 3] (4 total shards)
        foreach (var idx in usIndices.Union(euIndices))
        {
            Assert.InRange(idx, 0, 3);
        }
    }

    [Fact]
    public void GetRegionFromHeader_FallsBackToDefault_WhenMissing()
    {
        var router = CreateRouter(TwoRegionConfig());

        Assert.Equal("us", router.GetRegionFromHeader(null));
        Assert.Equal("us", router.GetRegionFromHeader(""));
        Assert.Equal("us", router.GetRegionFromHeader("   "));
        Assert.Equal("us", router.GetRegionFromHeader("unknown-region"));
    }

    [Fact]
    public void GetRegionFromHeader_RecognizesConfiguredRegions()
    {
        var router = CreateRouter(TwoRegionConfig());

        Assert.Equal("us", router.GetRegionFromHeader("us"));
        Assert.Equal("eu", router.GetRegionFromHeader("eu"));
        Assert.Equal("eu", router.GetRegionFromHeader("EU")); // case-insensitive
        Assert.Equal("us", router.GetRegionFromHeader("  US  ")); // trims whitespace
    }

    [Fact]
    public void FallsBackToDefaults_WhenNoConfigProvided()
    {
        var config = new Dictionary<string, string?>();
        var router = CreateRouter(config);

        Assert.Equal("default", router.DefaultRegion);
        Assert.Single(router.GetRegions());

        // Should still route deterministically with fallback
        var first = router.GetShardIndex("testCode", "default");
        var second = router.GetShardIndex("testCode", "default");
        Assert.Equal(first, second);
        Assert.InRange(first, 0, 1);
    }
}

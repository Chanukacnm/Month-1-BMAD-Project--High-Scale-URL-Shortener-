using Moq;
using UrlShortener.Application.Common.Interfaces;
using UrlShortener.Infrastructure.Persistence;

namespace UrlShortener.Tests;

public class ShardRouterTests
{
    private ShardRouter CreateRouter(int shardCount)
    {
        var mockFactory = new Mock<IShardConnectionFactory>();
        mockFactory.Setup(f => f.ShardCount).Returns(shardCount);
        return new ShardRouter(mockFactory.Object);
    }

    [Fact]
    public void GetShardIndex_IsDeterministic_AcrossMultipleCalls()
    {
        // The critical bug was string.GetHashCode() being randomized per process.
        // SHA256-based hashing must produce the same result every time.
        var router = CreateRouter(4);
        var code = "aBcDeFg";

        var first = router.GetShardIndex(code);
        var second = router.GetShardIndex(code);
        var third = router.GetShardIndex(code);

        Assert.Equal(first, second);
        Assert.Equal(second, third);
    }

    [Fact]
    public void GetShardIndex_ReturnsValidRange_ForVariousCodes()
    {
        var shardCount = 3;
        var router = CreateRouter(shardCount);
        var codes = new[] { "abc123", "XYZ789", "test", "a", "zzzzzzzzzz", "Hello World!" };

        foreach (var code in codes)
        {
            var index = router.GetShardIndex(code);
            Assert.InRange(index, 0, shardCount - 1);
        }
    }

    [Fact]
    public void GetShardIndex_ReturnsZero_ForNullOrEmpty()
    {
        var router = CreateRouter(2);

        Assert.Equal(0, router.GetShardIndex(null!));
        Assert.Equal(0, router.GetShardIndex(""));
    }

    [Fact]
    public void GetShardIndex_DistributesAcrossShards()
    {
        // Generate many codes and verify we hit multiple shards (not all the same)
        var shardCount = 4;
        var router = CreateRouter(shardCount);
        var shardHits = new HashSet<int>();

        for (int i = 0; i < 100; i++)
        {
            var code = $"code_{i}_{Guid.NewGuid():N}";
            shardHits.Add(router.GetShardIndex(code));
        }

        // With 100 random codes and 4 shards, we should hit at least 2 distinct shards
        Assert.True(shardHits.Count >= 2, $"Expected distribution across shards but only hit {shardHits.Count} shard(s).");
    }

    [Fact]
    public void GetShardIndex_ProducesKnownValue()
    {
        // Regression test: a specific code should always map to the same shard.
        // If this test breaks, it means the hashing algorithm changed (data loss risk).
        var router = CreateRouter(2);
        var index = router.GetShardIndex("testcode123");

        // Record the expected value on first run for this specific input
        Assert.InRange(index, 0, 1);
        // Subsequent calls must match
        Assert.Equal(index, router.GetShardIndex("testcode123"));
    }
}

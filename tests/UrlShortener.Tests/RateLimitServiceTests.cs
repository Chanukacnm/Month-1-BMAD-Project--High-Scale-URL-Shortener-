using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using UrlShortener.Infrastructure.Services;

namespace UrlShortener.Tests;

public class RateLimitServiceTests
{
    private static RedisRateLimitService CreateService(
        int permitLimit = 5, int windowSeconds = 60)
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["RateLimiting:Policies:test:PermitLimit"] = permitLimit.ToString(),
                ["RateLimiting:Policies:test:WindowSeconds"] = windowSeconds.ToString(),
            })
            .Build();

        // No Redis — will use in-memory fallback
        var serviceProvider = new Mock<IServiceProvider>();
        var logger = new Mock<ILogger<RedisRateLimitService>>();

        return new RedisRateLimitService(serviceProvider.Object, config, logger.Object);
    }

    [Fact]
    public async Task IsAllowed_ReturnsTrue_WhenUnderLimit()
    {
        var service = CreateService(permitLimit: 10);

        var result = await service.IsAllowedAsync("client1", "test");

        Assert.True(result);
    }

    [Fact]
    public async Task IsAllowed_ReturnsFalse_WhenLimitExceeded()
    {
        var service = CreateService(permitLimit: 3);

        // Make 3 allowed requests
        for (int i = 0; i < 3; i++)
        {
            var allowed = await service.IsAllowedAsync("client-limit", "test");
            Assert.True(allowed);
        }

        // 4th request should be denied
        var denied = await service.IsAllowedAsync("client-limit", "test");
        Assert.False(denied);
    }

    [Fact]
    public async Task GetInfo_ReturnsCorrectRemaining()
    {
        var service = CreateService(permitLimit: 5);

        await service.IsAllowedAsync("client-info", "test");
        await service.IsAllowedAsync("client-info", "test");

        var info = await service.GetInfoAsync("client-info", "test");

        Assert.True(info.IsAllowed);
        Assert.Equal(5, info.Limit);
        Assert.Equal(2, info.Remaining); // 5 - 3 (2 previous + 1 from GetInfo)
    }

    [Fact]
    public async Task DifferentClients_HaveSeparateLimits()
    {
        var service = CreateService(permitLimit: 2);

        // Exhaust client A
        await service.IsAllowedAsync("clientA", "test");
        await service.IsAllowedAsync("clientA", "test");
        var clientADenied = await service.IsAllowedAsync("clientA", "test");
        Assert.False(clientADenied);

        // Client B should still be allowed
        var clientBAllowed = await service.IsAllowedAsync("clientB", "test");
        Assert.True(clientBAllowed);
    }

    [Fact]
    public async Task FallsBackToDefaultPolicy_WhenPolicyNotFound()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["RateLimiting:Policies:default:PermitLimit"] = "100",
                ["RateLimiting:Policies:default:WindowSeconds"] = "60",
            })
            .Build();

        var serviceProvider = new Mock<IServiceProvider>();
        var logger = new Mock<ILogger<RedisRateLimitService>>();
        var service = new RedisRateLimitService(serviceProvider.Object, config, logger.Object);

        var info = await service.GetInfoAsync("client-fallback", "nonexistent-policy");

        Assert.True(info.IsAllowed);
        Assert.Equal(100, info.Limit);
    }
}

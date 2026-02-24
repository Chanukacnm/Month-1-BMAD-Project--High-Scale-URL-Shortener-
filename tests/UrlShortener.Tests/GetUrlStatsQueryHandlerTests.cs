using Microsoft.EntityFrameworkCore;
using Moq;
using UrlShortener.Application.Common.Interfaces;
using UrlShortener.Application.Urls.Queries;
using UrlShortener.Domain.Entities;
using UrlShortener.Infrastructure.Persistence;

namespace UrlShortener.Tests;

public class GetUrlStatsQueryHandlerTests
{
    private static ApplicationDbContext CreateInMemoryContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite("Data Source=:memory:")
            .Options;

        var context = new ApplicationDbContext(options);
        context.Database.OpenConnection();
        context.Database.EnsureCreated();
        return context;
    }

    [Fact]
    public async Task Handle_ReturnsStats_WhenUrlExists()
    {
        // Arrange
        var context = CreateInMemoryContext();
        var createdAt = DateTime.UtcNow;
        context.ShortUrls.Add(new ShortUrl
        {
            Id = Guid.NewGuid(),
            ShortCode = "stats01",
            OriginalUrl = "https://example.com",
            CreatedAt = createdAt
        });
        await context.SaveChangesAsync();

        var mockFactory = new Mock<IShardConnectionFactory>();
        mockFactory.Setup(f => f.ShardCount).Returns(1);
        mockFactory.Setup(f => f.CreateDbContext(It.IsAny<int>())).Returns(context);

        var mockRouter = new Mock<IShardRouter>();
        mockRouter.Setup(r => r.GetShardIndex(It.IsAny<string>())).Returns(0);

        var mockAnalytics = new Mock<IAnalyticsService>();
        mockAnalytics.Setup(a => a.GetClickCountAsync("stats01", It.IsAny<CancellationToken>()))
            .ReturnsAsync(42);

        var handler = new GetUrlStatsQueryHandler(mockFactory.Object, mockRouter.Object, mockAnalytics.Object);

        // Act
        var result = await handler.Handle(new GetUrlStatsQuery("stats01"), CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("stats01", result.ShortCode);
        Assert.Equal("https://example.com", result.OriginalUrl);
        Assert.Equal(42, result.TotalClicks);
    }

    [Fact]
    public async Task Handle_ReturnsNull_WhenUrlDoesNotExist()
    {
        // Arrange
        var context = CreateInMemoryContext();

        var mockFactory = new Mock<IShardConnectionFactory>();
        mockFactory.Setup(f => f.ShardCount).Returns(1);
        mockFactory.Setup(f => f.CreateDbContext(It.IsAny<int>())).Returns(context);

        var mockRouter = new Mock<IShardRouter>();
        mockRouter.Setup(r => r.GetShardIndex(It.IsAny<string>())).Returns(0);

        var mockAnalytics = new Mock<IAnalyticsService>();

        var handler = new GetUrlStatsQueryHandler(mockFactory.Object, mockRouter.Object, mockAnalytics.Object);

        // Act
        var result = await handler.Handle(new GetUrlStatsQuery("nonexistent"), CancellationToken.None);

        // Assert
        Assert.Null(result);
    }
}

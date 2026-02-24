using Microsoft.EntityFrameworkCore;
using Moq;
using UrlShortener.Application.Common.Interfaces;
using UrlShortener.Application.Urls.Commands;
using UrlShortener.Domain.Entities;
using UrlShortener.Infrastructure.Persistence;

namespace UrlShortener.Tests;

public class CreateShortUrlCommandHandlerTests
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
    public async Task Handle_CreatesShortUrl_Successfully()
    {
        // Arrange
        var context = CreateInMemoryContext();
        var mockFactory = new Mock<IShardConnectionFactory>();
        mockFactory.Setup(f => f.ShardCount).Returns(1);
        mockFactory.Setup(f => f.CreateDbContext(It.IsAny<int>())).Returns(context);

        var mockRouter = new Mock<IShardRouter>();
        mockRouter.Setup(r => r.GetShardIndex(It.IsAny<string>())).Returns(0);

        var mockCodeGen = new Mock<IShortCodeGenerator>();
        mockCodeGen.Setup(g => g.Generate(It.IsAny<int>())).Returns("abc1234");

        var handler = new CreateShortUrlCommandHandler(mockFactory.Object, mockRouter.Object, mockCodeGen.Object);

        // Act
        var result = await handler.Handle(new CreateShortUrlCommand("https://example.com"), CancellationToken.None);

        // Assert
        Assert.Equal("abc1234", result);
        Assert.Single(context.ShortUrls);
        var saved = await context.ShortUrls.FirstAsync();
        Assert.Equal("https://example.com", saved.OriginalUrl);
        Assert.Equal("abc1234", saved.ShortCode);
    }

    [Fact]
    public async Task Handle_RetriesOnCollision()
    {
        // Arrange
        var context = CreateInMemoryContext();

        // Pre-seed a colliding code
        context.ShortUrls.Add(new ShortUrl
        {
            Id = Guid.NewGuid(),
            ShortCode = "collide",
            OriginalUrl = "https://existing.com",
            CreatedAt = DateTime.UtcNow
        });
        await context.SaveChangesAsync();

        var mockFactory = new Mock<IShardConnectionFactory>();
        mockFactory.Setup(f => f.ShardCount).Returns(1);
        mockFactory.Setup(f => f.CreateDbContext(It.IsAny<int>())).Returns(context);

        var mockRouter = new Mock<IShardRouter>();
        mockRouter.Setup(r => r.GetShardIndex(It.IsAny<string>())).Returns(0);

        var callCount = 0;
        var mockCodeGen = new Mock<IShortCodeGenerator>();
        mockCodeGen.Setup(g => g.Generate(It.IsAny<int>()))
            .Returns(() =>
            {
                callCount++;
                return callCount == 1 ? "collide" : "unique7";
            });

        var handler = new CreateShortUrlCommandHandler(mockFactory.Object, mockRouter.Object, mockCodeGen.Object);

        // Act
        var result = await handler.Handle(new CreateShortUrlCommand("https://new.com"), CancellationToken.None);

        // Assert
        Assert.Equal("unique7", result);
        Assert.Equal(2, callCount); // First call collided, second succeeded
    }
}

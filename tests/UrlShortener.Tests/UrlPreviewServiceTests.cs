using Microsoft.Extensions.Logging;
using Moq;
using UrlShortener.Application.Common.Interfaces;
using UrlShortener.Application.Common.Models;
using UrlShortener.Infrastructure.Services;

namespace UrlShortener.Tests;

public class UrlPreviewServiceTests
{
    [Fact]
    public async Task GetPreviewAsync_ReturnsNull_ForEmptyUrl()
    {
        var mockFactory = new Mock<IHttpClientFactory>();
        var mockCache = new Mock<ICacheService>();
        var mockLogger = new Mock<ILogger<UrlPreviewService>>();

        var service = new UrlPreviewService(mockFactory.Object, mockCache.Object, mockLogger.Object);

        var result = await service.GetPreviewAsync("");

        Assert.Null(result);
    }

    [Fact]
    public async Task GetPreviewAsync_ReturnsNull_ForNonHttpUrl()
    {
        var mockFactory = new Mock<IHttpClientFactory>();
        var mockCache = new Mock<ICacheService>();
        var mockLogger = new Mock<ILogger<UrlPreviewService>>();

        var service = new UrlPreviewService(mockFactory.Object, mockCache.Object, mockLogger.Object);

        // ftp:// should be rejected (SSRF protection)
        var result = await service.GetPreviewAsync("ftp://evil.com/payload");

        Assert.Null(result);
    }

    [Fact]
    public async Task GetPreviewAsync_ReturnsCachedResult_WhenAvailable()
    {
        var cachedJson = System.Text.Json.JsonSerializer.Serialize(
            new UrlPreviewResult("Cached Title", "Cached Description", null, null, null, "https://example.com"));

        var mockFactory = new Mock<IHttpClientFactory>();
        var mockCache = new Mock<ICacheService>();
        mockCache.Setup(c => c.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(cachedJson);
        var mockLogger = new Mock<ILogger<UrlPreviewService>>();

        var service = new UrlPreviewService(mockFactory.Object, mockCache.Object, mockLogger.Object);

        var result = await service.GetPreviewAsync("https://example.com");

        Assert.NotNull(result);
        Assert.Equal("Cached Title", result!.Title);
        Assert.Equal("Cached Description", result.Description);
    }

    [Fact]
    public async Task GetPreviewAsync_ReturnsNull_ForFileProtocol()
    {
        var mockFactory = new Mock<IHttpClientFactory>();
        var mockCache = new Mock<ICacheService>();
        var mockLogger = new Mock<ILogger<UrlPreviewService>>();

        var service = new UrlPreviewService(mockFactory.Object, mockCache.Object, mockLogger.Object);

        // file:// should be rejected (SSRF protection)
        var result = await service.GetPreviewAsync("file:///etc/passwd");

        Assert.Null(result);
    }
}

using UrlShortener.Application.Common.Models;

namespace UrlShortener.Application.Common.Interfaces;

public interface IAnalyticsService
{
    Task TrackClickAsync(ClickMetadata metadata, CancellationToken cancellationToken = default);
    Task<long> GetClickCountAsync(string shortCode, CancellationToken cancellationToken = default);
}

using UrlShortener.Application.Common.Models;

namespace UrlShortener.Application.Common.Interfaces;

public interface IUrlPreviewService
{
    /// <summary>
    /// Fetches Open Graph metadata from the specified URL for rich link previews.
    /// Returns null if the URL is unreachable or has no parseable metadata.
    /// </summary>
    Task<UrlPreviewResult?> GetPreviewAsync(string url, CancellationToken cancellationToken = default);
}

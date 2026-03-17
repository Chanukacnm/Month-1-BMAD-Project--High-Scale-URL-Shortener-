namespace UrlShortener.Application.Common.Models;

/// <summary>
/// Open Graph metadata scraped from a URL for rich link previews.
/// </summary>
public record UrlPreviewResult(
    string? Title,
    string? Description,
    string? ImageUrl,
    string? SiteName,
    string? FaviconUrl,
    string OriginalUrl);

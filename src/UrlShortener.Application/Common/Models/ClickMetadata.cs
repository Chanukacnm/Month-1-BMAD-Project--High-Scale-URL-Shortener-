namespace UrlShortener.Application.Common.Models;

public record ClickMetadata(
    string ShortCode,
    string? IpAddress,
    string? UserAgent,
    string? Referer);

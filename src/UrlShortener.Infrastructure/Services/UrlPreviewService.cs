using System.Net.Http;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using UrlShortener.Application.Common.Interfaces;
using UrlShortener.Application.Common.Models;

namespace UrlShortener.Infrastructure.Services;

/// <summary>
/// Fetches and parses Open Graph metadata from URLs for rich link previews.
/// Includes safety measures: 5s timeout, 1MB response size limit, and
/// restricted to HTTP/HTTPS protocols to prevent SSRF attacks.
/// </summary>
public class UrlPreviewService : IUrlPreviewService
{
    private readonly HttpClient _httpClient;
    private readonly ICacheService _cacheService;
    private readonly ILogger<UrlPreviewService> _logger;

    private const int MaxResponseBytes = 1_048_576; // 1 MB
    private const string CacheKeyPrefix = "preview:";
    private static readonly TimeSpan CacheTtl = TimeSpan.FromHours(1);

    public UrlPreviewService(
        IHttpClientFactory httpClientFactory,
        ICacheService cacheService,
        ILogger<UrlPreviewService> logger)
    {
        _httpClient = httpClientFactory.CreateClient("UrlPreview");
        _cacheService = cacheService;
        _logger = logger;
    }

    public async Task<UrlPreviewResult?> GetPreviewAsync(string url, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(url))
            return null;

        // Security: Only allow HTTP/HTTPS
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) ||
            (uri.Scheme != "http" && uri.Scheme != "https"))
        {
            return null;
        }

        // Check cache first
        var cacheKey = $"{CacheKeyPrefix}{url}";
        var cached = await _cacheService.GetAsync(cacheKey, cancellationToken);
        if (cached != null)
        {
            return DeserializePreview(cached, url);
        }

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, uri);
            request.Headers.Add("User-Agent", "UrlShortener-Preview/1.0");
            request.Headers.Add("Accept", "text/html");

            using var response = await _httpClient.SendAsync(request,
                HttpCompletionOption.ResponseHeadersRead, cancellationToken);

            if (!response.IsSuccessStatusCode)
                return null;

            var contentType = response.Content.Headers.ContentType?.MediaType;
            if (contentType != null && !contentType.Contains("text/html"))
                return null;

            // Read with size limit
            var contentLength = response.Content.Headers.ContentLength;
            if (contentLength > MaxResponseBytes)
                return null;

            var html = await ReadWithLimitAsync(response.Content, cancellationToken);
            if (string.IsNullOrEmpty(html))
                return null;

            var preview = ParseOpenGraphTags(html, uri);

            // Cache the result
            if (preview != null)
            {
                var serialized = SerializePreview(preview);
                await _cacheService.SetAsync(cacheKey, serialized, CacheTtl, cancellationToken);
            }

            return preview;
        }
        catch (TaskCanceledException)
        {
            _logger.LogWarning("URL preview request timed out for {Url}", url);
            return null;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "Failed to fetch URL preview for {Url}", url);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during URL preview for {Url}", url);
            return null;
        }
    }

    private async Task<string?> ReadWithLimitAsync(HttpContent content, CancellationToken cancellationToken)
    {
        using var stream = await content.ReadAsStreamAsync(cancellationToken);
        using var reader = new StreamReader(stream);
        var buffer = new char[MaxResponseBytes];
        var charsRead = await reader.ReadAsync(buffer, 0, MaxResponseBytes);
        return charsRead > 0 ? new string(buffer, 0, charsRead) : null;
    }

    private static UrlPreviewResult? ParseOpenGraphTags(string html, Uri uri)
    {
        var title = ExtractMetaContent(html, "og:title")
                    ?? ExtractTagContent(html, "title");
        var description = ExtractMetaContent(html, "og:description")
                          ?? ExtractMetaName(html, "description");
        var imageUrl = ExtractMetaContent(html, "og:image");
        var siteName = ExtractMetaContent(html, "og:site_name");
        var faviconUrl = ExtractFavicon(html, uri);

        if (title == null && description == null && imageUrl == null)
            return null;

        return new UrlPreviewResult(
            Title: title,
            Description: description,
            ImageUrl: imageUrl,
            SiteName: siteName,
            FaviconUrl: faviconUrl,
            OriginalUrl: uri.ToString());
    }

    private static string? ExtractMetaContent(string html, string property)
    {
        var pattern = $@"<meta\s+[^>]*property\s*=\s*[""']{Regex.Escape(property)}[""'][^>]*content\s*=\s*[""']([^""']*)[""']";
        var match = Regex.Match(html, pattern, RegexOptions.IgnoreCase | RegexOptions.Singleline);
        if (match.Success) return match.Groups[1].Value;

        // Try reversed attribute order
        pattern = $@"<meta\s+[^>]*content\s*=\s*[""']([^""']*)[""'][^>]*property\s*=\s*[""']{Regex.Escape(property)}[""']";
        match = Regex.Match(html, pattern, RegexOptions.IgnoreCase | RegexOptions.Singleline);
        return match.Success ? match.Groups[1].Value : null;
    }

    private static string? ExtractMetaName(string html, string name)
    {
        var pattern = $@"<meta\s+[^>]*name\s*=\s*[""']{Regex.Escape(name)}[""'][^>]*content\s*=\s*[""']([^""']*)[""']";
        var match = Regex.Match(html, pattern, RegexOptions.IgnoreCase | RegexOptions.Singleline);
        if (match.Success) return match.Groups[1].Value;

        pattern = $@"<meta\s+[^>]*content\s*=\s*[""']([^""']*)[""'][^>]*name\s*=\s*[""']{Regex.Escape(name)}[""']";
        match = Regex.Match(html, pattern, RegexOptions.IgnoreCase | RegexOptions.Singleline);
        return match.Success ? match.Groups[1].Value : null;
    }

    private static string? ExtractTagContent(string html, string tag)
    {
        var pattern = $@"<{tag}[^>]*>([^<]*)</{tag}>";
        var match = Regex.Match(html, pattern, RegexOptions.IgnoreCase | RegexOptions.Singleline);
        return match.Success ? match.Groups[1].Value.Trim() : null;
    }

    private static string? ExtractFavicon(string html, Uri uri)
    {
        var pattern = @"<link\s+[^>]*rel\s*=\s*[""'](?:shortcut\s+)?icon[""'][^>]*href\s*=\s*[""']([^""']*)[""']";
        var match = Regex.Match(html, pattern, RegexOptions.IgnoreCase | RegexOptions.Singleline);

        if (match.Success)
        {
            var href = match.Groups[1].Value;
            if (Uri.TryCreate(uri, href, out var faviconUri))
                return faviconUri.ToString();
        }

        // Default favicon path
        return new Uri(uri, "/favicon.ico").ToString();
    }

    private static string SerializePreview(UrlPreviewResult preview)
    {
        return System.Text.Json.JsonSerializer.Serialize(preview);
    }

    private static UrlPreviewResult? DeserializePreview(string json, string fallbackUrl)
    {
        try
        {
            return System.Text.Json.JsonSerializer.Deserialize<UrlPreviewResult>(json);
        }
        catch
        {
            return null;
        }
    }
}

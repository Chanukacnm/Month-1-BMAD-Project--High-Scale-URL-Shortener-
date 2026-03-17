using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace UrlShortener.Api.Middleware;

/// <summary>
/// Middleware that records Prometheus-compatible metrics for every HTTP request:
/// request count, duration histogram, and cache hit/miss counters.
/// Uses System.Diagnostics.Metrics which is scraped by prometheus-net.
/// </summary>
public class MetricsMiddleware
{
    private readonly RequestDelegate _next;

    // Application-level metrics using System.Diagnostics.Metrics
    private static readonly Meter AppMeter = new("UrlShortener", "1.0.0");

    private static readonly Counter<long> RequestCounter =
        AppMeter.CreateCounter<long>("url_shortener_requests_total",
            description: "Total number of HTTP requests");

    private static readonly Histogram<double> RequestDuration =
        AppMeter.CreateHistogram<double>("url_shortener_request_duration_seconds",
            unit: "s",
            description: "HTTP request duration in seconds");

    private static readonly Counter<long> CacheHitCounter =
        AppMeter.CreateCounter<long>("url_shortener_cache_hits_total",
            description: "Total cache hits");

    private static readonly Counter<long> CacheMissCounter =
        AppMeter.CreateCounter<long>("url_shortener_cache_misses_total",
            description: "Total cache misses");

    private static readonly Counter<long> RateLimitRejectCounter =
        AppMeter.CreateCounter<long>("url_shortener_rate_limit_rejections_total",
            description: "Total rate limit rejections");

    public MetricsMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var stopwatch = Stopwatch.StartNew();

        await _next(context);

        stopwatch.Stop();
        var elapsed = stopwatch.Elapsed.TotalSeconds;

        var method = context.Request.Method;
        var endpoint = ClassifyEndpoint(context.Request.Path.Value);
        var statusCode = context.Response.StatusCode.ToString();

        var tags = new TagList
        {
            { "method", method },
            { "endpoint", endpoint },
            { "status_code", statusCode }
        };

        RequestCounter.Add(1, tags);
        RequestDuration.Record(elapsed, tags);

        // Track rate limit rejections
        if (context.Response.StatusCode == 429)
        {
            RateLimitRejectCounter.Add(1, new TagList { { "endpoint", endpoint } });
        }
    }

    private static string ClassifyEndpoint(string? path)
    {
        if (string.IsNullOrEmpty(path)) return "unknown";
        if (path.StartsWith("/api/shorten")) return "/api/shorten";
        if (path.StartsWith("/api/stats")) return "/api/stats";
        if (path.StartsWith("/api/preview")) return "/api/preview";
        if (path.StartsWith("/api/urls")) return "/api/urls";
        if (path == "/health") return "/health";
        if (path == "/metrics") return "/metrics";
        if (path.Length > 1 && !path.StartsWith("/api/")) return "/redirect";
        return "other";
    }

    /// <summary>
    /// Static helpers for other services to record cache metrics.
    /// </summary>
    public static void RecordCacheHit(string endpoint = "redirect")
    {
        CacheHitCounter.Add(1, new TagList { { "endpoint", endpoint } });
    }

    public static void RecordCacheMiss(string endpoint = "redirect")
    {
        CacheMissCounter.Add(1, new TagList { { "endpoint", endpoint } });
    }
}

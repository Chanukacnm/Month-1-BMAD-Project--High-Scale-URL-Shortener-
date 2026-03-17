using UrlShortener.Application.Common.Interfaces;

namespace UrlShortener.Api.Middleware;

/// <summary>
/// Rate limiting middleware that enforces per-IP request limits using
/// configurable policies. Returns 429 Too Many Requests with standard
/// rate limit headers when a client exceeds their quota.
/// </summary>
public class RateLimitingMiddleware
{
    private readonly RequestDelegate _next;

    public RateLimitingMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, IRateLimitService rateLimitService)
    {
        // Skip rate limiting for health checks and metrics
        var path = context.Request.Path.Value?.ToLowerInvariant() ?? "";
        if (path == "/health" || path == "/metrics")
        {
            await _next(context);
            return;
        }

        var clientId = GetClientIdentifier(context);
        var policy = DeterminePolicy(context);

        var rateLimitInfo = await rateLimitService.GetInfoAsync(clientId, policy);

        // Always set rate limit headers
        context.Response.Headers["X-RateLimit-Limit"] = rateLimitInfo.Limit.ToString();
        context.Response.Headers["X-RateLimit-Remaining"] = rateLimitInfo.Remaining.ToString();

        if (!rateLimitInfo.IsAllowed)
        {
            context.Response.Headers["Retry-After"] = rateLimitInfo.RetryAfterSeconds.ToString();
            context.Response.StatusCode = StatusCodes.Status429TooManyRequests;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync(
                $"{{\"error\":\"Rate limit exceeded. Retry after {rateLimitInfo.RetryAfterSeconds} seconds.\",\"retryAfter\":{rateLimitInfo.RetryAfterSeconds}}}");
            return;
        }

        await _next(context);
    }

    private static string GetClientIdentifier(HttpContext context)
    {
        // Use X-Forwarded-For if behind a proxy, otherwise use RemoteIpAddress
        var forwardedFor = context.Request.Headers["X-Forwarded-For"].FirstOrDefault();
        if (!string.IsNullOrEmpty(forwardedFor))
        {
            return forwardedFor.Split(',')[0].Trim();
        }

        return context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
    }

    private static string DeterminePolicy(HttpContext context)
    {
        var path = context.Request.Path.Value ?? "";
        var method = context.Request.Method;

        // URL creation
        if (method == HttpMethods.Post && path.StartsWith("/api/shorten", StringComparison.OrdinalIgnoreCase))
        {
            return "create";
        }

        // URL redirects (root-level GET with a code)
        if (method == HttpMethods.Get && !path.StartsWith("/api/") && path.Length > 1)
        {
            return "redirect";
        }

        return "default";
    }
}

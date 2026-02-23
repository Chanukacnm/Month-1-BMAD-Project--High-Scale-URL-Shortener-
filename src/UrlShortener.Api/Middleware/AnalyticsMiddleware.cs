using Microsoft.AspNetCore.Http;
using UrlShortener.Application.Common.Interfaces;
using UrlShortener.Application.Common.Models;

namespace UrlShortener.Api.Middleware;

public class AnalyticsMiddleware
{
    private readonly RequestDelegate _next;

    public AnalyticsMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, IAnalyticsService analyticsService)
    {
        // Intercept GET requests that look like redirects (root path with a code)
        // For simplicity in this prototype, we check if it's a GET and not an API call
        var path = context.Request.Path.Value;
        
        // We only want to track root-level redirects, not /api/...
        if (context.Request.Method == HttpMethods.Get && 
            !string.IsNullOrEmpty(path) && 
            !path.StartsWith("/api/") && 
            path.Length > 1)
        {
            var shortCode = path.TrimStart('/');
            
            // Extract metadata
            var metadata = new ClickMetadata(
                shortCode,
                context.Connection.RemoteIpAddress?.ToString(),
                context.Request.Headers["User-Agent"],
                context.Request.Headers["Referer"]
            );

            // Trigger tracking (Fire and forget or awaited? For now, we await but in story 2.3 we'll make it async)
            await analyticsService.TrackClickAsync(metadata);
        }

        await _next(context);
    }
}

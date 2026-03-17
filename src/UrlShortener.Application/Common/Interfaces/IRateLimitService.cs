namespace UrlShortener.Application.Common.Interfaces;

public interface IRateLimitService
{
    /// <summary>
    /// Checks if the client is allowed to make a request under the given policy.
    /// Returns true if allowed, false if rate limited.
    /// </summary>
    Task<bool> IsAllowedAsync(string clientId, string policy, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets rate limit info for a client under a given policy.
    /// </summary>
    Task<RateLimitInfo> GetInfoAsync(string clientId, string policy, CancellationToken cancellationToken = default);
}

public record RateLimitInfo(
    bool IsAllowed,
    int Limit,
    int Remaining,
    int RetryAfterSeconds);

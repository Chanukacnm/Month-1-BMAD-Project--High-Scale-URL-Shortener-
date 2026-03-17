namespace UrlShortener.Application.Common.Interfaces;

public interface IGeoShardRouter
{
    /// <summary>
    /// Gets the shard index for a short code within a specific geographic region.
    /// </summary>
    int GetShardIndex(string shortCode, string region);

    /// <summary>
    /// Determines the geographic region from an HTTP request using the X-Geo-Region header.
    /// Falls back to the default region if the header is missing or the region is unknown.
    /// </summary>
    string GetRegionFromHeader(string? geoRegionHeader);

    /// <summary>
    /// Returns all configured region names.
    /// </summary>
    IReadOnlyList<string> GetRegions();

    /// <summary>
    /// Returns the default region name.
    /// </summary>
    string DefaultRegion { get; }
}

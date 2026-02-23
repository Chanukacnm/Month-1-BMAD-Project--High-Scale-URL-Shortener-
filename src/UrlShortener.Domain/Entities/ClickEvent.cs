namespace UrlShortener.Domain.Entities;

public class ClickEvent
{
    public Guid Id { get; set; }
    public string ShortCode { get; set; } = string.Empty;
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
    public string? Referer { get; set; }
    public DateTime OccurredAt { get; set; }
}

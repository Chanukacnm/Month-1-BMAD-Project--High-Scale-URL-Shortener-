using MediatR;
using Microsoft.EntityFrameworkCore;
using UrlShortener.Application.Common.Interfaces;

namespace UrlShortener.Application.Urls.Queries;

public record GetUrlStatsQuery(string ShortCode) : IRequest<UrlStatsResult?>;

public record UrlStatsResult(
    string ShortCode,
    string OriginalUrl,
    long TotalClicks,
    DateTime CreatedAt);

public class GetUrlStatsQueryHandler : IRequestHandler<GetUrlStatsQuery, UrlStatsResult?>
{
    private readonly IShardConnectionFactory _contextFactory;
    private readonly IShardRouter _router;
    private readonly IAnalyticsService _analyticsService;

    public GetUrlStatsQueryHandler(
        IShardConnectionFactory contextFactory,
        IShardRouter router,
        IAnalyticsService analyticsService)
    {
        _contextFactory = contextFactory;
        _router = router;
        _analyticsService = analyticsService;
    }

    public async Task<UrlStatsResult?> Handle(GetUrlStatsQuery request, CancellationToken cancellationToken)
    {
        var shardIndex = _router.GetShardIndex(request.ShortCode);
        var context = _contextFactory.CreateDbContext(shardIndex);

        var shortUrl = await context.ShortUrls
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.ShortCode == request.ShortCode, cancellationToken);

        if (shortUrl == null)
        {
            return null;
        }

        var totalClicks = await _analyticsService.GetClickCountAsync(request.ShortCode, cancellationToken);

        return new UrlStatsResult(
            shortUrl.ShortCode,
            shortUrl.OriginalUrl,
            totalClicks,
            shortUrl.CreatedAt);
    }
}

using MediatR;
using Microsoft.EntityFrameworkCore;
using UrlShortener.Application.Common.Interfaces;

namespace UrlShortener.Application.Urls.Queries;

public record GetOriginalUrlQuery(string ShortCode) : IRequest<string?>;

public class GetOriginalUrlQueryHandler : IRequestHandler<GetOriginalUrlQuery, string?>
{
    private readonly IShardConnectionFactory _contextFactory;
    private readonly IShardRouter _router;
    private readonly ICacheService _cacheService;

    public GetOriginalUrlQueryHandler(IShardConnectionFactory contextFactory, IShardRouter router, ICacheService cacheService)
    {
        _contextFactory = contextFactory;
        _router = router;
        _cacheService = cacheService;
    }

    public async Task<string?> Handle(GetOriginalUrlQuery request, CancellationToken cancellationToken)
    {
        var cacheKey = $"url:{request.ShortCode}";

        // 1. Try Cache
        var cachedUrl = await _cacheService.GetAsync(cacheKey, cancellationToken);
        if (cachedUrl != null)
        {
            return cachedUrl;
        }

        // 2. Fallback to Sharded DB
        var shardIndex = _router.GetShardIndex(request.ShortCode);
        var context = _contextFactory.CreateDbContext(shardIndex);

        var shortUrl = await context.ShortUrls
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.ShortCode == request.ShortCode, cancellationToken);

        if (shortUrl != null)
        {
            // 3. Populate Cache (TTL 24h)
            await _cacheService.SetAsync(cacheKey, shortUrl.OriginalUrl, TimeSpan.FromHours(24), cancellationToken);
            return shortUrl.OriginalUrl;
        }

        return null;
    }
}

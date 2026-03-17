using MediatR;
using Microsoft.EntityFrameworkCore;
using UrlShortener.Application.Common.Interfaces;
using UrlShortener.Application.Common.Models;

namespace UrlShortener.Application.Urls.Queries;

public record GetUrlPreviewQuery(string ShortCode) : IRequest<UrlPreviewResult?>;

public class GetUrlPreviewQueryHandler : IRequestHandler<GetUrlPreviewQuery, UrlPreviewResult?>
{
    private readonly IShardConnectionFactory _contextFactory;
    private readonly IShardRouter _router;
    private readonly IUrlPreviewService _previewService;

    public GetUrlPreviewQueryHandler(
        IShardConnectionFactory contextFactory,
        IShardRouter router,
        IUrlPreviewService previewService)
    {
        _contextFactory = contextFactory;
        _router = router;
        _previewService = previewService;
    }

    public async Task<UrlPreviewResult?> Handle(GetUrlPreviewQuery request, CancellationToken cancellationToken)
    {
        // 1. Look up the original URL from the sharded DB
        var shardIndex = _router.GetShardIndex(request.ShortCode);
        var context = _contextFactory.CreateDbContext(shardIndex);

        var shortUrl = await context.ShortUrls
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.ShortCode == request.ShortCode, cancellationToken);

        if (shortUrl == null)
        {
            return null;
        }

        // 2. Fetch preview metadata (cached internally by the service)
        return await _previewService.GetPreviewAsync(shortUrl.OriginalUrl, cancellationToken);
    }
}

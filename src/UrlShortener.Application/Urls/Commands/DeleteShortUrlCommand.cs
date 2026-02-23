using MediatR;
using Microsoft.EntityFrameworkCore;
using UrlShortener.Application.Common.Interfaces;

namespace UrlShortener.Application.Urls.Commands;

public record DeleteShortUrlCommand(string ShortCode) : IRequest<bool>;

public class DeleteShortUrlCommandHandler : IRequestHandler<DeleteShortUrlCommand, bool>
{
    private readonly IShardConnectionFactory _contextFactory;
    private readonly IShardRouter _router;
    private readonly ICacheService _cacheService;

    public DeleteShortUrlCommandHandler(IShardConnectionFactory contextFactory, IShardRouter router, ICacheService cacheService)
    {
        _contextFactory = contextFactory;
        _router = router;
        _cacheService = cacheService;
    }

    public async Task<bool> Handle(DeleteShortUrlCommand request, CancellationToken cancellationToken)
    {
        var shardIndex = _router.GetShardIndex(request.ShortCode);
        var context = _contextFactory.CreateDbContext(shardIndex);

        var shortUrl = await context.ShortUrls
            .FirstOrDefaultAsync(x => x.ShortCode == request.ShortCode, cancellationToken);

        if (shortUrl == null)
        {
            return false;
        }

        context.ShortUrls.Remove(shortUrl);
        await context.SaveChangesAsync(cancellationToken);

        // Invalidate Cache
        await _cacheService.RemoveAsync($"url:{request.ShortCode}", cancellationToken);

        return true;
    }
}

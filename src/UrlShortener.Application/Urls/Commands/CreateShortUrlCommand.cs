using MediatR;
using Microsoft.EntityFrameworkCore;
using UrlShortener.Application.Common.Interfaces;
using UrlShortener.Domain.Entities;

namespace UrlShortener.Application.Urls.Commands;

public record CreateShortUrlCommand(string OriginalUrl) : IRequest<string>;

public class CreateShortUrlCommandHandler : IRequestHandler<CreateShortUrlCommand, string>
{
    private readonly IShardConnectionFactory _contextFactory;
    private readonly IShardRouter _router;
    private readonly IShortCodeGenerator _codeGenerator;

    public CreateShortUrlCommandHandler(IShardConnectionFactory contextFactory, IShardRouter router, IShortCodeGenerator codeGenerator)
    {
        _contextFactory = contextFactory;
        _router = router;
        _codeGenerator = codeGenerator;
    }

    public async Task<string> Handle(CreateShortUrlCommand request, CancellationToken cancellationToken)
    {
        // Simple collision handling logic
        string shortCode;
        bool exists;
        int retries = 0;

        do
        {
            shortCode = _codeGenerator.Generate();
            
            var shardIndex = _router.GetShardIndex(shortCode);
            var context = _contextFactory.CreateDbContext(shardIndex);

            exists = await context.ShortUrls.AnyAsync(x => x.ShortCode == shortCode, cancellationToken);
            
            if (!exists)
            {
                var shortUrl = new ShortUrl
                {
                    Id = Guid.NewGuid(),
                    ShortCode = shortCode,
                    OriginalUrl = request.OriginalUrl,
                    CreatedAt = DateTime.UtcNow
                };

                context.ShortUrls.Add(shortUrl);
                await context.SaveChangesAsync(cancellationToken);
            }

            retries++;
        } while (exists && retries < 5);

        if (exists)
        {
            throw new Exception("Failed to generate a unique short code after several attempts.");
        }

        return shortCode;
    }
}

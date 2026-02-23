using Microsoft.EntityFrameworkCore;
using UrlShortener.Domain.Entities;

namespace UrlShortener.Application.Common.Interfaces;

public interface IApplicationDbContext
{
    DbSet<ShortUrl> ShortUrls { get; }
    DbSet<ClickEvent> ClickEvents { get; }
    Task<int> SaveChangesAsync(CancellationToken cancellationToken);
}

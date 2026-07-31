using Microsoft.EntityFrameworkCore;
using TenderScope.Application.Contracts;
using TenderScope.Domain.Entities;

namespace TenderScope.Infrastructure.Persistence;

public sealed class TenderSourceRepository(TenderScopeDbContext dbContext) : ITenderSourceRepository
{
    public async Task<IReadOnlyList<TenderSource>> GetDueAsync(DateTimeOffset now, CancellationToken cancellationToken) =>
        await dbContext.Sources
            .Where(x => x.IsEnabled && (x.NextCrawlAt == null || x.NextCrawlAt <= now))
            .OrderBy(x => x.NextCrawlAt)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<TenderSource>> ListAsync(CancellationToken cancellationToken) =>
        await dbContext.Sources.AsNoTracking().OrderBy(x => x.Name).ToListAsync(cancellationToken);

    public Task<TenderSource?> FindByKeyAsync(string key, CancellationToken cancellationToken) =>
        dbContext.Sources.SingleOrDefaultAsync(x => x.Key == key, cancellationToken);

    public Task AddAsync(TenderSource source, CancellationToken cancellationToken) =>
        dbContext.Sources.AddAsync(source, cancellationToken).AsTask();

    public Task SaveChangesAsync(CancellationToken cancellationToken) => dbContext.SaveChangesAsync(cancellationToken);
}

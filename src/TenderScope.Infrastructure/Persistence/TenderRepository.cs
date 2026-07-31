using Microsoft.EntityFrameworkCore;
using TenderScope.Application.Contracts;
using TenderScope.Domain.Entities;

namespace TenderScope.Infrastructure.Persistence;

public sealed class TenderRepository(TenderScopeDbContext dbContext) : ITenderRepository
{
    public async Task<IReadOnlyList<Tender>> SearchAsync(string? query, string? country, string? category, int take, CancellationToken cancellationToken)
    {
        IQueryable<Tender> tenders = dbContext.Tenders.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(query)) tenders = tenders.Where(x => EF.Functions.ILike(x.Title, $"%{query}%") || (x.Description != null && EF.Functions.ILike(x.Description, $"%{query}%")));
        if (!string.IsNullOrWhiteSpace(country)) tenders = tenders.Where(x => x.CountryCode == country.ToUpper());
        if (!string.IsNullOrWhiteSpace(category)) tenders = tenders.Where(x => x.Category == category);
        return await tenders.OrderByDescending(x => x.PublishedAt).Take(Math.Clamp(take, 1, 100)).ToListAsync(cancellationToken);
    }

    public Task<Tender?> FindBySourceAsync(string sourceKey, string externalId, CancellationToken cancellationToken) =>
        dbContext.Tenders.SingleOrDefaultAsync(x => x.SourceKey == sourceKey && x.ExternalId == externalId, cancellationToken);

    public async Task UpsertAsync(Tender tender, CancellationToken cancellationToken)
    {
        var existing = await FindBySourceAsync(tender.SourceKey, tender.ExternalId, cancellationToken);
        if (existing is null) await dbContext.Tenders.AddAsync(tender, cancellationToken);
        else existing.MarkObserved(tender.ContentHash, DateTimeOffset.UtcNow);
    }

    public Task<int> CountAsync(CancellationToken cancellationToken) => dbContext.Tenders.CountAsync(cancellationToken);
    public Task SaveChangesAsync(CancellationToken cancellationToken) => dbContext.SaveChangesAsync(cancellationToken);
}

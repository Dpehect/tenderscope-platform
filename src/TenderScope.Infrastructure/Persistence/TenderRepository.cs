using Microsoft.EntityFrameworkCore;
using TenderScope.Application.Contracts;
using TenderScope.Application.Models;
using TenderScope.Domain.Entities;

namespace TenderScope.Infrastructure.Persistence;

public sealed class TenderRepository(TenderScopeDbContext dbContext) : ITenderRepository
{
    public async Task<IReadOnlyList<Tender>> SearchAsync(string? query, string? country, string? category, int take, CancellationToken cancellationToken)
    {
        var result = await SearchAdvancedAsync(query, country, category, null, null, null, null, "published-desc", 1, take, cancellationToken);
        return result.Items;
    }

    public async Task<TenderSearchResult> SearchAdvancedAsync(string? query, string? country, string? category, DateTimeOffset? deadlineFrom, DateTimeOffset? deadlineTo, decimal? minValue, decimal? maxValue, string sort, int page, int pageSize, CancellationToken cancellationToken)
    {
        IQueryable<Tender> filtered = dbContext.Tenders.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(query)) filtered = filtered.Where(x => EF.Functions.ILike(x.Title, $"%{query}%") || EF.Functions.ILike(x.BuyerName, $"%{query}%") || (x.Description != null && EF.Functions.ILike(x.Description, $"%{query}%")));
        if (!string.IsNullOrWhiteSpace(country)) filtered = filtered.Where(x => x.CountryCode == country.ToUpper());
        if (!string.IsNullOrWhiteSpace(category)) filtered = filtered.Where(x => x.Category == category);
        if (deadlineFrom.HasValue) filtered = filtered.Where(x => x.DeadlineAt >= deadlineFrom);
        if (deadlineTo.HasValue) filtered = filtered.Where(x => x.DeadlineAt <= deadlineTo);
        if (minValue.HasValue) filtered = filtered.Where(x => x.EstimatedValue >= minValue);
        if (maxValue.HasValue) filtered = filtered.Where(x => x.EstimatedValue <= maxValue);

        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, 100);
        var total = await filtered.CountAsync(cancellationToken);
        var countries = await filtered.GroupBy(x => x.CountryCode).Select(x => new { x.Key, Count = x.Count() }).ToDictionaryAsync(x => x.Key, x => x.Count, cancellationToken);
        var categories = await filtered.Where(x => x.Category != null).GroupBy(x => x.Category!).Select(x => new { x.Key, Count = x.Count() }).ToDictionaryAsync(x => x.Key, x => x.Count, cancellationToken);

        filtered = sort switch
        {
            "deadline-asc" => filtered.OrderBy(x => x.DeadlineAt == null).ThenBy(x => x.DeadlineAt),
            "value-desc" => filtered.OrderByDescending(x => x.EstimatedValue),
            "value-asc" => filtered.OrderBy(x => x.EstimatedValue == null).ThenBy(x => x.EstimatedValue),
            _ => filtered.OrderByDescending(x => x.PublishedAt)
        };

        var items = await filtered.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(cancellationToken);
        return new TenderSearchResult(items, total, page, pageSize, countries, categories);
    }

    public Task<Tender?> FindBySourceAsync(string sourceKey, string externalId, CancellationToken cancellationToken) =>
        dbContext.Tenders.SingleOrDefaultAsync(x => x.SourceKey == sourceKey && x.ExternalId == externalId, cancellationToken);

    public Task<Tender?> FindByFingerprintAsync(string contentHash, CancellationToken cancellationToken) =>
        dbContext.Tenders.FirstOrDefaultAsync(x => x.ContentHash == contentHash, cancellationToken);

    public async Task UpsertAsync(Tender tender, CancellationToken cancellationToken)
    {
        var existing = await FindBySourceAsync(tender.SourceKey, tender.ExternalId, cancellationToken)
            ?? await FindByFingerprintAsync(tender.ContentHash, cancellationToken);
        if (existing is null) await dbContext.Tenders.AddAsync(tender, cancellationToken);
        else existing.MarkObserved(tender.ContentHash, DateTimeOffset.UtcNow);
    }

    public Task<int> CountAsync(CancellationToken cancellationToken) => dbContext.Tenders.CountAsync(cancellationToken);
    public Task SaveChangesAsync(CancellationToken cancellationToken) => dbContext.SaveChangesAsync(cancellationToken);
}

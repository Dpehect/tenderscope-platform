using Microsoft.EntityFrameworkCore;
using TenderScope.Application.Contracts;
using TenderScope.Domain.Entities;

namespace TenderScope.Infrastructure.Persistence;

public sealed class WorkspaceRepository(TenderScopeDbContext db) : IWorkspaceRepository
{
    public async Task<IReadOnlyList<WorkspaceItem>> ListItemsAsync(Guid organizationId, CancellationToken cancellationToken) =>
        await db.WorkspaceItems.AsNoTracking()
            .Where(x => x.OrganizationId == organizationId)
            .OrderBy(x => x.Stage).ThenBy(x => x.Position).ThenByDescending(x => x.UpdatedAt)
            .ToListAsync(cancellationToken);

    public async Task<WorkspaceItem> SaveItemAsync(Guid organizationId, Guid userId, Guid tenderId, OpportunityStage stage, string? notes, CancellationToken cancellationToken)
    {
        var item = await db.WorkspaceItems.SingleOrDefaultAsync(x => x.OrganizationId == organizationId && x.TenderId == tenderId, cancellationToken);
        if (item is null)
        {
            item = new WorkspaceItem { OrganizationId = organizationId, CreatedByUserId = userId, TenderId = tenderId };
            item.Update(stage, notes);
            var lastPosition = await db.WorkspaceItems.Where(x => x.OrganizationId == organizationId && x.Stage == stage)
                .Select(x => (decimal?)x.Position).MaxAsync(cancellationToken) ?? 0;
            item.Move(stage, lastPosition + 1000);
            await db.WorkspaceItems.AddAsync(item, cancellationToken);
        }
        else item.Update(stage, notes);
        return item;
    }

    public async Task RemoveItemAsync(Guid organizationId, Guid tenderId, CancellationToken cancellationToken)
    {
        var item = await db.WorkspaceItems.SingleOrDefaultAsync(x => x.OrganizationId == organizationId && x.TenderId == tenderId, cancellationToken);
        if (item is not null) db.WorkspaceItems.Remove(item);
    }

    public async Task<IReadOnlyList<SavedSearch>> ListSearchesAsync(Guid organizationId, CancellationToken cancellationToken) =>
        await db.SavedSearches.AsNoTracking().Where(x => x.OrganizationId == organizationId).OrderByDescending(x => x.CreatedAt).ToListAsync(cancellationToken);

    public async Task<SavedSearch> AddSearchAsync(Guid organizationId, Guid userId, SavedSearch search, CancellationToken cancellationToken)
    {
        var entity = new SavedSearch { OrganizationId = organizationId, CreatedByUserId = userId, Name = search.Name, Query = search.Query, Country = search.Country, Category = search.Category };
        entity.SetNotifications(search.NotificationsEnabled);
        await db.SavedSearches.AddAsync(entity, cancellationToken);
        return entity;
    }

    public async Task RemoveSearchAsync(Guid organizationId, Guid id, CancellationToken cancellationToken)
    {
        var search = await db.SavedSearches.SingleOrDefaultAsync(x => x.OrganizationId == organizationId && x.Id == id, cancellationToken);
        if (search is not null) db.SavedSearches.Remove(search);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken) => db.SaveChangesAsync(cancellationToken);
}

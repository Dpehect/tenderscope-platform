using Microsoft.EntityFrameworkCore;
using TenderScope.Application.Contracts;
using TenderScope.Domain.Entities;

namespace TenderScope.Infrastructure.Persistence;

public sealed class WorkspaceRepository(TenderScopeDbContext db) : IWorkspaceRepository
{
    public async Task<IReadOnlyList<WorkspaceItem>> ListItemsAsync(string userKey, CancellationToken cancellationToken) =>
        await db.WorkspaceItems.AsNoTracking().Where(x => x.UserKey == userKey).OrderByDescending(x => x.UpdatedAt).ToListAsync(cancellationToken);

    public async Task<WorkspaceItem> SaveItemAsync(string userKey, Guid tenderId, OpportunityStage stage, string? notes, CancellationToken cancellationToken)
    {
        var item = await db.WorkspaceItems.SingleOrDefaultAsync(x => x.UserKey == userKey && x.TenderId == tenderId, cancellationToken);
        if (item is null)
        {
            item = new WorkspaceItem { UserKey = userKey, TenderId = tenderId };
            item.Update(stage, notes);
            await db.WorkspaceItems.AddAsync(item, cancellationToken);
        }
        else item.Update(stage, notes);
        return item;
    }

    public async Task RemoveItemAsync(string userKey, Guid tenderId, CancellationToken cancellationToken)
    {
        var item = await db.WorkspaceItems.SingleOrDefaultAsync(x => x.UserKey == userKey && x.TenderId == tenderId, cancellationToken);
        if (item is not null) db.WorkspaceItems.Remove(item);
    }

    public async Task<IReadOnlyList<SavedSearch>> ListSearchesAsync(string userKey, CancellationToken cancellationToken) =>
        await db.SavedSearches.AsNoTracking().Where(x => x.UserKey == userKey).OrderByDescending(x => x.CreatedAt).ToListAsync(cancellationToken);

    public async Task<SavedSearch> AddSearchAsync(SavedSearch search, CancellationToken cancellationToken)
    {
        await db.SavedSearches.AddAsync(search, cancellationToken);
        return search;
    }

    public async Task RemoveSearchAsync(string userKey, Guid id, CancellationToken cancellationToken)
    {
        var search = await db.SavedSearches.SingleOrDefaultAsync(x => x.UserKey == userKey && x.Id == id, cancellationToken);
        if (search is not null) db.SavedSearches.Remove(search);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken) => db.SaveChangesAsync(cancellationToken);
}

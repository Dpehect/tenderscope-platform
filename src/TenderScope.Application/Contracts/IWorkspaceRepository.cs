using TenderScope.Domain.Entities;

namespace TenderScope.Application.Contracts;

public interface IWorkspaceRepository
{
    Task<IReadOnlyList<WorkspaceItem>> ListItemsAsync(string userKey, CancellationToken cancellationToken);
    Task<WorkspaceItem> SaveItemAsync(string userKey, Guid tenderId, OpportunityStage stage, string? notes, CancellationToken cancellationToken);
    Task RemoveItemAsync(string userKey, Guid tenderId, CancellationToken cancellationToken);
    Task<IReadOnlyList<SavedSearch>> ListSearchesAsync(string userKey, CancellationToken cancellationToken);
    Task<SavedSearch> AddSearchAsync(SavedSearch search, CancellationToken cancellationToken);
    Task RemoveSearchAsync(string userKey, Guid id, CancellationToken cancellationToken);
    Task SaveChangesAsync(CancellationToken cancellationToken);
}

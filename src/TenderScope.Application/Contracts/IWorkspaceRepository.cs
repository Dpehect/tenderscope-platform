using TenderScope.Domain.Entities;

namespace TenderScope.Application.Contracts;

public interface IWorkspaceRepository
{
    Task<IReadOnlyList<WorkspaceItem>> ListItemsAsync(Guid organizationId, CancellationToken cancellationToken);
    Task<WorkspaceItem> SaveItemAsync(Guid organizationId, Guid userId, Guid tenderId, OpportunityStage stage, string? notes, CancellationToken cancellationToken);
    Task RemoveItemAsync(Guid organizationId, Guid tenderId, CancellationToken cancellationToken);
    Task<IReadOnlyList<SavedSearch>> ListSearchesAsync(Guid organizationId, CancellationToken cancellationToken);
    Task<SavedSearch> AddSearchAsync(Guid organizationId, Guid userId, SavedSearch search, CancellationToken cancellationToken);
    Task RemoveSearchAsync(Guid organizationId, Guid id, CancellationToken cancellationToken);
    Task SaveChangesAsync(CancellationToken cancellationToken);
}

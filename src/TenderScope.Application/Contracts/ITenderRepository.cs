using TenderScope.Domain.Entities;

namespace TenderScope.Application.Contracts;

public interface ITenderRepository
{
    Task<IReadOnlyList<Tender>> SearchAsync(string? query, string? country, string? category, int take, CancellationToken cancellationToken);
    Task<Tender?> FindBySourceAsync(string sourceKey, string externalId, CancellationToken cancellationToken);
    Task UpsertAsync(Tender tender, CancellationToken cancellationToken);
    Task<int> CountAsync(CancellationToken cancellationToken);
    Task SaveChangesAsync(CancellationToken cancellationToken);
}

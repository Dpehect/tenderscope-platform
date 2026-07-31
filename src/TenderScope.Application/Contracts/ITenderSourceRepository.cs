using TenderScope.Domain.Entities;

namespace TenderScope.Application.Contracts;

public interface ITenderSourceRepository
{
    Task<IReadOnlyList<TenderSource>> GetDueAsync(DateTimeOffset now, CancellationToken cancellationToken);
    Task<IReadOnlyList<TenderSource>> ListAsync(CancellationToken cancellationToken);
    Task<TenderSource?> FindByKeyAsync(string key, CancellationToken cancellationToken);
    Task AddAsync(TenderSource source, CancellationToken cancellationToken);
    Task SaveChangesAsync(CancellationToken cancellationToken);
}

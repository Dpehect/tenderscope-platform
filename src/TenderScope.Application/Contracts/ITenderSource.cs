using TenderScope.Domain.Entities;

namespace TenderScope.Application.Contracts;

public interface ITenderSource
{
    string Key { get; }
    IAsyncEnumerable<Tender> FetchAsync(CancellationToken cancellationToken);
}

using TenderScope.Domain.Entities;

namespace TenderScope.Application.Models;

public sealed record TenderSearchResult(
    IReadOnlyList<Tender> Items,
    int Total,
    int Page,
    int PageSize,
    IReadOnlyDictionary<string, int> Countries,
    IReadOnlyDictionary<string, int> Categories);

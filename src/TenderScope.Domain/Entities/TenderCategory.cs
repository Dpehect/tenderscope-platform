namespace TenderScope.Domain.Entities;

public sealed class TenderCategory
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public required string Code { get; init; }
    public required string Name { get; init; }
    public string? ParentCode { get; init; }
}

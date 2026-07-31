namespace TenderScope.Domain.Entities;

public sealed class TenderDocument
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid TenderId { get; init; }
    public required string Name { get; init; }
    public required Uri Url { get; init; }
    public string? MediaType { get; init; }
    public string? ContentHash { get; init; }
    public DateTimeOffset DiscoveredAt { get; init; } = DateTimeOffset.UtcNow;
}

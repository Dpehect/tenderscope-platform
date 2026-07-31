namespace TenderScope.Domain.Entities;

public sealed class Tender
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public required string ExternalId { get; init; }
    public required string SourceKey { get; init; }
    public required string Title { get; init; }
    public required string BuyerName { get; init; }
    public required string CountryCode { get; init; }
    public string? Region { get; init; }
    public string? Description { get; init; }
    public string? Category { get; init; }
    public decimal? EstimatedValue { get; init; }
    public string? Currency { get; init; }
    public DateTimeOffset PublishedAt { get; init; }
    public DateTimeOffset? DeadlineAt { get; init; }
    public required Uri SourceUrl { get; init; }
    public string ContentHash { get; private set; } = string.Empty;
    public DateTimeOffset LastSeenAt { get; private set; } = DateTimeOffset.UtcNow;

    public void MarkObserved(string contentHash, DateTimeOffset observedAt)
    {
        ContentHash = contentHash;
        LastSeenAt = observedAt;
    }
}

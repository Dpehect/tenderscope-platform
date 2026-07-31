namespace TenderScope.Domain.Entities;

public sealed class AuditLog
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public required string Action { get; init; }
    public required string Resource { get; init; }
    public string? ActorKey { get; init; }
    public string? Detail { get; init; }
    public string? IpAddress { get; init; }
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
}

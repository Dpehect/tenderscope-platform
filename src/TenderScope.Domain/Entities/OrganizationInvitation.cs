namespace TenderScope.Domain.Entities;

public sealed class OrganizationInvitation
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid OrganizationId { get; init; }
    public required string Email { get; init; }
    public OrganizationRole Role { get; init; } = OrganizationRole.Viewer;
    public required string TokenHash { get; init; }
    public Guid InvitedByUserId { get; init; }
    public DateTimeOffset CreatedAt { get; private set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset ExpiresAt { get; init; }
    public DateTimeOffset? AcceptedAt { get; private set; }
    public DateTimeOffset? RevokedAt { get; private set; }
    public bool IsActive => AcceptedAt is null && RevokedAt is null && ExpiresAt > DateTimeOffset.UtcNow;
    public void Accept(DateTimeOffset now) => AcceptedAt = now;
    public void Revoke(DateTimeOffset now) => RevokedAt = now;
}

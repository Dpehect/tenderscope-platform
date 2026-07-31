namespace TenderScope.Domain.Entities;

public enum OrganizationRole { Viewer, Analyst, Manager, Admin, Owner }

public sealed class Organization
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public required string Name { get; init; }
    public required string Slug { get; init; }
    public DateTimeOffset CreatedAt { get; private set; } = DateTimeOffset.UtcNow;
    public bool IsActive { get; private set; } = true;
    public void Deactivate() => IsActive = false;
}

public sealed class AppUser
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public required string Email { get; init; }
    public required string DisplayName { get; init; }
    public required string PasswordHash { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? LastLoginAt { get; private set; }
    public bool IsActive { get; private set; } = true;

    public static AppUser Create(string email, string displayName, string passwordHash) => new()
    {
        Email = email.Trim().ToLowerInvariant(),
        DisplayName = displayName.Trim(),
        PasswordHash = passwordHash
    };

    public void ChangePassword(string passwordHash) => PasswordHash = passwordHash;
    public void MarkLogin(DateTimeOffset now) => LastLoginAt = now;
    public void Deactivate() => IsActive = false;
}

public sealed class OrganizationMembership
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid OrganizationId { get; init; }
    public Guid UserId { get; init; }
    public OrganizationRole Role { get; private set; } = OrganizationRole.Viewer;
    public DateTimeOffset JoinedAt { get; private set; } = DateTimeOffset.UtcNow;
    public void ChangeRole(OrganizationRole role) => Role = role;
}

public sealed class RefreshToken
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid UserId { get; init; }
    public Guid? OrganizationId { get; init; }
    public required string TokenHash { get; init; }
    public DateTimeOffset CreatedAt { get; private set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset ExpiresAt { get; init; }
    public DateTimeOffset? RevokedAt { get; private set; }
    public string? ReplacedByTokenHash { get; private set; }
    public bool IsActive => RevokedAt is null && ExpiresAt > DateTimeOffset.UtcNow;
    public void Revoke(DateTimeOffset now, string? replacementHash = null) { RevokedAt = now; ReplacedByTokenHash = replacementHash; }
}

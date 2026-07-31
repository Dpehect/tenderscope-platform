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
    public int FailedLoginCount { get; private set; }
    public DateTimeOffset? LockedUntil { get; private set; }
    public bool IsActive { get; private set; } = true;
    public bool IsLocked(DateTimeOffset now) => LockedUntil > now;

    public static AppUser Create(string email, string displayName, string passwordHash) => new()
    {
        Email = email.Trim().ToLowerInvariant(),
        DisplayName = displayName.Trim(),
        PasswordHash = passwordHash
    };

    public void ChangePassword(string passwordHash)
    {
        PasswordHash = passwordHash;
        FailedLoginCount = 0;
        LockedUntil = null;
    }

    public void RegisterFailedLogin(DateTimeOffset now)
    {
        FailedLoginCount++;
        if (FailedLoginCount >= 5)
        {
            LockedUntil = now.AddMinutes(Math.Min(60, 5 * Math.Pow(2, FailedLoginCount - 5)));
        }
    }

    public void MarkLogin(DateTimeOffset now)
    {
        LastLoginAt = now;
        FailedLoginCount = 0;
        LockedUntil = null;
    }

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
    public Guid FamilyId { get; init; } = Guid.NewGuid();
    public required string TokenHash { get; init; }
    public string? IpAddress { get; init; }
    public string? UserAgent { get; init; }
    public DateTimeOffset CreatedAt { get; private set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset LastUsedAt { get; private set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset ExpiresAt { get; init; }
    public DateTimeOffset? RevokedAt { get; private set; }
    public string? ReplacedByTokenHash { get; private set; }
    public bool IsActive => RevokedAt is null && ExpiresAt > DateTimeOffset.UtcNow;
    public void Touch(DateTimeOffset now) => LastUsedAt = now;
    public void Revoke(DateTimeOffset now, string? replacementHash = null) { RevokedAt = now; ReplacedByTokenHash = replacementHash; }
}

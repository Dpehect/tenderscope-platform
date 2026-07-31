namespace TenderScope.Domain.Entities;

public sealed class WatchlistMatch
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid OrganizationId { get; init; }
    public Guid SavedSearchId { get; init; }
    public Guid TenderId { get; init; }
    public int Score { get; init; }
    public required string Reason { get; init; }
    public DateTimeOffset CreatedAt { get; private set; } = DateTimeOffset.UtcNow;
}

public sealed class AppNotification
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid OrganizationId { get; init; }
    public Guid UserId { get; init; }
    public required string Type { get; init; }
    public required string Title { get; init; }
    public required string Message { get; init; }
    public string? ResourceUrl { get; init; }
    public DateTimeOffset CreatedAt { get; private set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? ReadAt { get; private set; }
    public bool IsRead => ReadAt.HasValue;
    public void MarkRead(DateTimeOffset now) => ReadAt ??= now;
}

public sealed class NotificationPreference
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid OrganizationId { get; init; }
    public Guid UserId { get; init; }
    public bool InAppEnabled { get; private set; } = true;
    public bool WatchlistMatchesEnabled { get; private set; } = true;
    public bool DeadlineRemindersEnabled { get; private set; } = true;
    public DateTimeOffset UpdatedAt { get; private set; } = DateTimeOffset.UtcNow;
    public void Update(bool inAppEnabled, bool watchlistMatchesEnabled, bool deadlineRemindersEnabled)
    {
        InAppEnabled = inAppEnabled;
        WatchlistMatchesEnabled = watchlistMatchesEnabled;
        DeadlineRemindersEnabled = deadlineRemindersEnabled;
        UpdatedAt = DateTimeOffset.UtcNow;
    }
}

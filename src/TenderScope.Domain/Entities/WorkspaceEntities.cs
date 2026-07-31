namespace TenderScope.Domain.Entities;

public enum OpportunityStage { Review, Qualified, Preparing, Submitted, Won, Lost }

public sealed class WorkspaceItem
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public required string UserKey { get; init; }
    public required Guid TenderId { get; init; }
    public OpportunityStage Stage { get; private set; } = OpportunityStage.Review;
    public string? Notes { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; private set; } = DateTimeOffset.UtcNow;

    public void Update(OpportunityStage stage, string? notes)
    {
        Stage = stage;
        Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim()[..Math.Min(notes.Trim().Length, 4000)];
        UpdatedAt = DateTimeOffset.UtcNow;
    }
}

public sealed class SavedSearch
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public required string UserKey { get; init; }
    public required string Name { get; init; }
    public string? Query { get; init; }
    public string? Country { get; init; }
    public string? Category { get; init; }
    public bool NotificationsEnabled { get; private set; } = true;
    public DateTimeOffset CreatedAt { get; private set; } = DateTimeOffset.UtcNow;

    public void SetNotifications(bool enabled) => NotificationsEnabled = enabled;
}

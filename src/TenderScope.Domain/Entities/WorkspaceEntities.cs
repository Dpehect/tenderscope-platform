namespace TenderScope.Domain.Entities;

public enum OpportunityStage { Review, Qualified, Preparing, Submitted, Won, Lost }

public sealed class WorkspaceItem
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid OrganizationId { get; init; }
    public Guid CreatedByUserId { get; init; }
    public required Guid TenderId { get; init; }
    public OpportunityStage Stage { get; private set; } = OpportunityStage.Review;
    public decimal Position { get; private set; }
    public string? Notes { get; private set; }
    public string[] Tags { get; private set; } = [];
    public DateTimeOffset? InternalDeadline { get; private set; }
    public Guid? AssigneeUserId { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; private set; } = DateTimeOffset.UtcNow;

    public void Update(OpportunityStage stage, string? notes)
    {
        Stage = stage;
        Notes = NormalizeNotes(notes);
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void Configure(string? notes, IEnumerable<string>? tags, DateTimeOffset? internalDeadline, Guid? assigneeUserId)
    {
        Notes = NormalizeNotes(notes);
        Tags = (tags ?? [])
            .Select(x => x.Trim().ToLowerInvariant())
            .Where(x => x.Length is > 0 and <= 40)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(12)
            .ToArray();
        InternalDeadline = internalDeadline;
        AssigneeUserId = assigneeUserId;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void Move(OpportunityStage stage, decimal position)
    {
        Stage = stage;
        Position = Math.Max(0, position);
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    private static string? NormalizeNotes(string? notes) =>
        string.IsNullOrWhiteSpace(notes) ? null : notes.Trim()[..Math.Min(notes.Trim().Length, 4000)];
}

public sealed class WorkspaceActivity
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid OrganizationId { get; init; }
    public Guid WorkspaceItemId { get; init; }
    public Guid ActorUserId { get; init; }
    public required string Action { get; init; }
    public string? Detail { get; init; }
    public DateTimeOffset CreatedAt { get; private set; } = DateTimeOffset.UtcNow;
}

public sealed class SavedSearch
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid OrganizationId { get; init; }
    public Guid CreatedByUserId { get; init; }
    public required string Name { get; init; }
    public string? Query { get; init; }
    public string? Country { get; init; }
    public string? Category { get; init; }
    public bool NotificationsEnabled { get; private set; } = true;
    public DateTimeOffset CreatedAt { get; private set; } = DateTimeOffset.UtcNow;

    public void SetNotifications(bool enabled) => NotificationsEnabled = enabled;
}

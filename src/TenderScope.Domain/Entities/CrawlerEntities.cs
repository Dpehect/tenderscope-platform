namespace TenderScope.Domain.Entities;

public enum CrawlRunStatus { Running, Succeeded, PartiallySucceeded, Failed }

public sealed class CrawlRun
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid SourceId { get; init; }
    public required string SourceKey { get; init; }
    public required string ParserVersion { get; init; }
    public CrawlRunStatus Status { get; private set; } = CrawlRunStatus.Running;
    public int Attempt { get; init; } = 1;
    public int FetchedCount { get; private set; }
    public int ImportedCount { get; private set; }
    public int RejectedCount { get; private set; }
    public long DurationMilliseconds { get; private set; }
    public string? Error { get; private set; }
    public DateTimeOffset StartedAt { get; init; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? CompletedAt { get; private set; }

    public void Complete(int fetched, int imported, int rejected, TimeSpan duration)
    {
        FetchedCount = Math.Max(0, fetched);
        ImportedCount = Math.Max(0, imported);
        RejectedCount = Math.Max(0, rejected);
        DurationMilliseconds = Math.Max(0, (long)duration.TotalMilliseconds);
        Status = rejected > 0 ? CrawlRunStatus.PartiallySucceeded : CrawlRunStatus.Succeeded;
        CompletedAt = DateTimeOffset.UtcNow;
    }

    public void Fail(Exception exception, TimeSpan duration)
    {
        DurationMilliseconds = Math.Max(0, (long)duration.TotalMilliseconds);
        Error = exception.Message[..Math.Min(exception.Message.Length, 4000)];
        Status = CrawlRunStatus.Failed;
        CompletedAt = DateTimeOffset.UtcNow;
    }
}

public sealed class CrawlDeadLetter
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid? CrawlRunId { get; init; }
    public Guid SourceId { get; init; }
    public required string SourceKey { get; init; }
    public string? ExternalId { get; init; }
    public required string Error { get; init; }
    public string? PayloadPreview { get; init; }
    public int Attempts { get; private set; } = 1;
    public DateTimeOffset CreatedAt { get; private set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? ResolvedAt { get; private set; }

    public void IncrementAttempt() => Attempts++;
    public void Resolve() => ResolvedAt = DateTimeOffset.UtcNow;
}

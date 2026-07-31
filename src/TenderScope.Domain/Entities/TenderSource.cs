namespace TenderScope.Domain.Entities;

public enum SourceFormat { Html, Rss, Atom, Xml, Json, Csv }
public enum SourceHealth { Healthy, Degraded, Failing, Disabled }

public sealed class TenderSource
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public required string Key { get; init; }
    public required string Name { get; init; }
    public required Uri BaseUrl { get; init; }
    public required SourceFormat Format { get; init; }
    public string CountryCode { get; init; } = "INT";
    public bool IsEnabled { get; private set; } = true;
    public int CrawlIntervalMinutes { get; private set; } = 360;
    public SourceHealth Health { get; private set; } = SourceHealth.Healthy;
    public int ConsecutiveFailures { get; private set; }
    public DateTimeOffset? LastAttemptAt { get; private set; }
    public DateTimeOffset? LastSuccessAt { get; private set; }
    public DateTimeOffset? NextCrawlAt { get; private set; }
    public string? LastError { get; private set; }

    public void ConfigureInterval(int minutes) => CrawlIntervalMinutes = Math.Clamp(minutes, 15, 1440);
    public void Schedule(DateTimeOffset now) => NextCrawlAt = now.AddMinutes(CrawlIntervalMinutes);
    public void MarkSucceeded(DateTimeOffset now)
    {
        LastAttemptAt = LastSuccessAt = now;
        ConsecutiveFailures = 0;
        LastError = null;
        Health = SourceHealth.Healthy;
        Schedule(now);
    }
    public void MarkFailed(DateTimeOffset now, string error)
    {
        LastAttemptAt = now;
        ConsecutiveFailures++;
        LastError = error[..Math.Min(error.Length, 2000)];
        Health = ConsecutiveFailures >= 5 ? SourceHealth.Failing : SourceHealth.Degraded;
        var delay = Math.Min(CrawlIntervalMinutes * Math.Pow(2, Math.Min(ConsecutiveFailures, 4)), 1440);
        NextCrawlAt = now.AddMinutes(delay);
    }
    public void SetEnabled(bool enabled)
    {
        IsEnabled = enabled;
        Health = enabled ? SourceHealth.Healthy : SourceHealth.Disabled;
    }
}

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
    public string ParserVersion { get; private set; } = "1.0.0";
    public decimal QualityScore { get; private set; } = 100m;
    public decimal SuccessRate { get; private set; } = 100m;
    public int TotalRuns { get; private set; }
    public int SuccessfulRuns { get; private set; }
    public DateTimeOffset? LastDataAt { get; private set; }

    public void ConfigureInterval(int minutes) => CrawlIntervalMinutes = Math.Clamp(minutes, 15, 1440);
    public void ConfigureParserVersion(string version) => ParserVersion = string.IsNullOrWhiteSpace(version) ? "1.0.0" : version.Trim()[..Math.Min(version.Trim().Length, 40)];
    public void Schedule(DateTimeOffset now) => NextCrawlAt = now.AddMinutes(CrawlIntervalMinutes);

    public void MarkSucceeded(DateTimeOffset now, int imported = 0)
    {
        LastAttemptAt = LastSuccessAt = now;
        if (imported > 0) LastDataAt = now;
        ConsecutiveFailures = 0;
        LastError = null;
        Health = SourceHealth.Healthy;
        TotalRuns++;
        SuccessfulRuns++;
        RecalculateQuality(imported > 0);
        Schedule(now);
    }

    public void MarkFailed(DateTimeOffset now, string error)
    {
        LastAttemptAt = now;
        ConsecutiveFailures++;
        LastError = error[..Math.Min(error.Length, 2000)];
        Health = ConsecutiveFailures >= 5 ? SourceHealth.Failing : SourceHealth.Degraded;
        TotalRuns++;
        RecalculateQuality(false);
        var delay = Math.Min(CrawlIntervalMinutes * Math.Pow(2, Math.Min(ConsecutiveFailures, 4)), 1440);
        NextCrawlAt = now.AddMinutes(delay);
    }

    public void SetEnabled(bool enabled)
    {
        IsEnabled = enabled;
        Health = enabled ? SourceHealth.Healthy : SourceHealth.Disabled;
    }

    private void RecalculateQuality(bool producedData)
    {
        SuccessRate = TotalRuns == 0 ? 100m : Math.Round(SuccessfulRuns * 100m / TotalRuns, 2);
        var freshness = LastDataAt is null ? 35m : Math.Max(0m, 100m - (decimal)(DateTimeOffset.UtcNow - LastDataAt.Value).TotalDays * 2m);
        var stability = Math.Max(0m, 100m - ConsecutiveFailures * 15m);
        var production = producedData ? 100m : 60m;
        QualityScore = Math.Round(SuccessRate * .45m + freshness * .25m + stability * .20m + production * .10m, 2);
    }
}

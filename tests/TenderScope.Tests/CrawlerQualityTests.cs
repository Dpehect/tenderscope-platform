using TenderScope.Domain.Entities;

namespace TenderScope.Tests;

public sealed class CrawlerQualityTests
{
    [Fact]
    public void Successful_runs_improve_source_quality_and_reset_failures()
    {
        var source = CreateSource();
        source.MarkFailed(DateTimeOffset.UtcNow, "temporary failure");
        source.MarkSucceeded(DateTimeOffset.UtcNow.AddMinutes(1), 25);

        Assert.Equal(SourceHealth.Healthy, source.Health);
        Assert.Equal(0, source.ConsecutiveFailures);
        Assert.Equal(2, source.TotalRuns);
        Assert.Equal(1, source.SuccessfulRuns);
        Assert.Equal(50m, source.SuccessRate);
        Assert.NotNull(source.LastDataAt);
        Assert.InRange(source.QualityScore, 0m, 100m);
    }

    [Fact]
    public void Repeated_failures_apply_backoff_and_reduce_quality()
    {
        var source = CreateSource();
        var now = DateTimeOffset.UtcNow;
        for (var i = 0; i < 5; i++) source.MarkFailed(now.AddMinutes(i), $"failure-{i}");

        Assert.Equal(SourceHealth.Failing, source.Health);
        Assert.Equal(5, source.ConsecutiveFailures);
        Assert.Equal(0m, source.SuccessRate);
        Assert.True(source.NextCrawlAt > now.AddHours(1));
        Assert.True(source.QualityScore < 50m);
    }

    [Fact]
    public void Crawl_run_records_partial_success()
    {
        var run = new CrawlRun { SourceId = Guid.NewGuid(), SourceKey = "test", ParserVersion = "2.0.0" };
        run.Complete(20, 18, 2, TimeSpan.FromSeconds(3));

        Assert.Equal(CrawlRunStatus.PartiallySucceeded, run.Status);
        Assert.Equal(20, run.FetchedCount);
        Assert.Equal(18, run.ImportedCount);
        Assert.Equal(2, run.RejectedCount);
        Assert.NotNull(run.CompletedAt);
    }

    private static TenderSource CreateSource() => new()
    {
        Key = $"source-{Guid.NewGuid():N}",
        Name = "Test source",
        BaseUrl = new Uri("https://example.com"),
        Format = SourceFormat.Json
    };
}

using TenderScope.Domain.Entities;
using Xunit;

namespace TenderScope.Domain.Tests;

public sealed class TenderSourceTests
{
    private static TenderSource CreateSource() => new()
    {
        Key = "test-source",
        Name = "Test Source",
        BaseUrl = new Uri("https://example.org"),
        Format = SourceFormat.Json,
        CountryCode = "TR"
    };

    [Fact]
    public void Successful_crawl_resets_failures_and_schedules_next_run()
    {
        var source = CreateSource();
        var now = DateTimeOffset.UtcNow;
        source.MarkFailed(now, "temporary");
        source.MarkSucceeded(now.AddMinutes(1));
        Assert.Equal(0, source.ConsecutiveFailures);
        Assert.Equal(SourceHealth.Healthy, source.Health);
        Assert.NotNull(source.NextCrawlAt);
        Assert.Null(source.LastError);
    }

    [Fact]
    public void Repeated_failures_move_source_to_failing_state()
    {
        var source = CreateSource();
        for (var index = 0; index < 5; index++) source.MarkFailed(DateTimeOffset.UtcNow, "failure");
        Assert.Equal(SourceHealth.Failing, source.Health);
        Assert.Equal(5, source.ConsecutiveFailures);
    }

    [Fact]
    public void Disabled_source_reports_disabled_health()
    {
        var source = CreateSource();
        source.SetEnabled(false);
        Assert.False(source.IsEnabled);
        Assert.Equal(SourceHealth.Disabled, source.Health);
    }
}

using TenderScope.Api.Auth;
using TenderScope.Domain.Entities;

namespace TenderScope.Tests;

public sealed class DomainTests
{
    [Fact]
    public void Password_hash_roundtrip_is_valid()
    {
        var service = new PasswordService();
        var hash = service.Hash("StrongPassword123!");
        Assert.True(service.Verify("StrongPassword123!", hash));
        Assert.False(service.Verify("WrongPassword", hash));
    }

    [Fact]
    public void Workspace_tags_are_normalized_and_limited()
    {
        var item = new WorkspaceItem { OrganizationId = Guid.NewGuid(), CreatedByUserId = Guid.NewGuid(), TenderId = Guid.NewGuid() };
        item.Configure(" note ", ["Priority", "priority", "Review"], null, null);
        Assert.Equal("note", item.Notes);
        Assert.Equal(["priority", "review"], item.Tags);
    }

    [Fact]
    public void Tender_source_interval_is_clamped()
    {
        var source = new TenderSource { Key = "test", Name = "Test", BaseUrl = new Uri("https://example.com"), Format = SourceFormat.Json };
        source.ConfigureInterval(1);
        Assert.Equal(15, source.CrawlIntervalMinutes);
        source.ConfigureInterval(5000);
        Assert.Equal(1440, source.CrawlIntervalMinutes);
    }
}

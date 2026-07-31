using Microsoft.EntityFrameworkCore;
using TenderScope.Domain.Entities;
using TenderScope.Infrastructure.Persistence;
using Xunit;

namespace TenderScope.Tests;

[CollectionDefinition("postgres", DisableParallelization = true)]
public sealed class PostgresCollection : ICollectionFixture<PostgresFixture>
{
}

public sealed class PostgresFixture : IAsyncLifetime
{
    public string ConnectionString { get; } = Environment.GetEnvironmentVariable("TEST_POSTGRES")
        ?? "Host=localhost;Port=5432;Database=tenderscope_test;Username=postgres;Password=postgres";

    public TenderScopeDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<TenderScopeDbContext>()
            .UseNpgsql(ConnectionString)
            .EnableDetailedErrors()
            .Options;
        return new TenderScopeDbContext(options);
    }

    public async Task InitializeAsync()
    {
        await using var db = CreateDb();
        await db.Database.EnsureDeletedAsync();
        await db.Database.EnsureCreatedAsync();
        await db.EnsureIdentitySchemaAsync();
        await db.EnsureAccountRecoverySchemaAsync();
        await db.EnsureWorkspaceTenantSchemaAsync();
        await db.EnsureNotificationSchemaAsync();
        await db.ApplyProductionMigrationsAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;
}

[Collection("postgres")]
public sealed class PostgresTenantIsolationTests(PostgresFixture fixture)
{
    [Fact]
    public async Task Workspace_repository_only_returns_requested_organization()
    {
        await using var db = fixture.CreateDb();
        var seed = await SeedTwoOrganizationsAsync(db);
        var repository = new WorkspaceRepository(db);

        await repository.SaveItemAsync(seed.OrganizationA.Id, seed.UserA.Id, seed.TenderA.Id, OpportunityStage.Qualified, "A only", default);
        await repository.SaveItemAsync(seed.OrganizationB.Id, seed.UserB.Id, seed.TenderB.Id, OpportunityStage.Submitted, "B only", default);
        await repository.SaveChangesAsync(default);

        var aItems = await repository.ListItemsAsync(seed.OrganizationA.Id, default);
        var bItems = await repository.ListItemsAsync(seed.OrganizationB.Id, default);

        Assert.Single(aItems);
        Assert.Single(bItems);
        Assert.Equal(seed.OrganizationA.Id, aItems[0].OrganizationId);
        Assert.Equal(seed.TenderA.Id, aItems[0].TenderId);
        Assert.Equal(seed.OrganizationB.Id, bItems[0].OrganizationId);
        Assert.Equal(seed.TenderB.Id, bItems[0].TenderId);
    }

    [Fact]
    public async Task Organization_cannot_delete_another_organizations_workspace_item()
    {
        await using var db = fixture.CreateDb();
        var seed = await SeedTwoOrganizationsAsync(db);
        var repository = new WorkspaceRepository(db);

        await repository.SaveItemAsync(seed.OrganizationB.Id, seed.UserB.Id, seed.TenderB.Id, OpportunityStage.Review, null, default);
        await repository.SaveChangesAsync(default);

        await repository.RemoveItemAsync(seed.OrganizationA.Id, seed.TenderB.Id, default);
        await repository.SaveChangesAsync(default);

        Assert.True(await db.WorkspaceItems.AnyAsync(x => x.OrganizationId == seed.OrganizationB.Id && x.TenderId == seed.TenderB.Id));
    }

    [Fact]
    public async Task Saved_searches_are_tenant_scoped()
    {
        await using var db = fixture.CreateDb();
        var seed = await SeedTwoOrganizationsAsync(db);
        var repository = new WorkspaceRepository(db);

        await repository.AddSearchAsync(seed.OrganizationA.Id, seed.UserA.Id, SavedSearch.Create(seed.OrganizationA.Id, seed.UserA.Id, "A search", "software", "TR", null, true), default);
        await repository.AddSearchAsync(seed.OrganizationB.Id, seed.UserB.Id, SavedSearch.Create(seed.OrganizationB.Id, seed.UserB.Id, "B search", "construction", "DE", null, true), default);
        await repository.SaveChangesAsync(default);

        var searches = await repository.ListSearchesAsync(seed.OrganizationA.Id, default);
        Assert.Single(searches);
        Assert.Equal("A search", searches[0].Name);
        Assert.Equal(seed.OrganizationA.Id, searches[0].OrganizationId);
    }

    [Fact]
    public async Task Production_migrations_are_idempotent_and_ledger_is_unique()
    {
        await using var db = fixture.CreateDb();
        await db.ApplyProductionMigrationsAsync();
        await db.ApplyProductionMigrationsAsync();

        var applied = await db.Database
            .SqlQueryRaw<string>("SELECT version AS \"Value\" FROM schema_migrations ORDER BY version")
            .ToListAsync();

        Assert.NotEmpty(applied);
        Assert.Equal(applied.Count, applied.Distinct(StringComparer.Ordinal).Count());
    }

    private static async Task<Seed> SeedTwoOrganizationsAsync(TenderScopeDbContext db)
    {
        await db.WorkspaceItems.ExecuteDeleteAsync();
        await db.SavedSearches.ExecuteDeleteAsync();
        await db.OrganizationMemberships.ExecuteDeleteAsync();
        await db.RefreshTokens.ExecuteDeleteAsync();
        await db.Users.ExecuteDeleteAsync();
        await db.Organizations.ExecuteDeleteAsync();
        await db.Tenders.ExecuteDeleteAsync();

        var orgA = new Organization { Name = "Organization A", Slug = $"organization-a-{Guid.NewGuid():N}" };
        var orgB = new Organization { Name = "Organization B", Slug = $"organization-b-{Guid.NewGuid():N}" };
        var userA = AppUser.Create($"a-{Guid.NewGuid():N}@example.com", "User A", "test-hash");
        var userB = AppUser.Create($"b-{Guid.NewGuid():N}@example.com", "User B", "test-hash");
        var tenderA = Tender("A");
        var tenderB = Tender("B");

        db.AddRange(orgA, orgB, userA, userB, tenderA, tenderB);
        db.OrganizationMemberships.AddRange(
            new OrganizationMembership { OrganizationId = orgA.Id, UserId = userA.Id },
            new OrganizationMembership { OrganizationId = orgB.Id, UserId = userB.Id });
        await db.SaveChangesAsync();
        return new Seed(orgA, orgB, userA, userB, tenderA, tenderB);
    }

    private static Tender Tender(string suffix) => new()
    {
        ExternalId = $"test-{suffix}-{Guid.NewGuid():N}",
        SourceKey = "integration-test",
        Title = $"Tender {suffix}",
        BuyerName = $"Buyer {suffix}",
        CountryCode = "TR",
        PublishedAt = DateTimeOffset.UtcNow,
        SourceUrl = new Uri($"https://example.com/{suffix.ToLowerInvariant()}")
    };

    private sealed record Seed(Organization OrganizationA, Organization OrganizationB, AppUser UserA, AppUser UserB, Tender TenderA, Tender TenderB);
}

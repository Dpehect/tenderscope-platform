using Microsoft.EntityFrameworkCore;
using TenderScope.Application.Contracts;
using TenderScope.Domain.Entities;
using TenderScope.Infrastructure;
using TenderScope.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddOpenApi();
builder.Services.AddCors(options => options.AddDefaultPolicy(policy => policy.AllowAnyHeader().AllowAnyMethod().AllowAnyOrigin()));

var app = builder.Build();
app.UseCors();
app.MapOpenApi();
app.MapGet("/health", () => Results.Ok(new { status = "healthy", service = "tenderscope-api", utc = DateTimeOffset.UtcNow }));
app.MapGet("/api/tenders", async (string? q, string? country, string? category, DateTimeOffset? deadlineFrom, DateTimeOffset? deadlineTo, decimal? minValue, decimal? maxValue, string? sort, int? page, int? pageSize, ITenderRepository repository, CancellationToken cancellationToken) =>
    Results.Ok(await repository.SearchAdvancedAsync(q, country, category, deadlineFrom, deadlineTo, minValue, maxValue, sort ?? "published-desc", page ?? 1, pageSize ?? 30, cancellationToken)));
app.MapGet("/api/sources", async (ITenderSourceRepository repository, CancellationToken cancellationToken) =>
    Results.Ok(await repository.ListAsync(cancellationToken)));
app.MapGet("/api/stats", async (ITenderRepository tenders, ITenderSourceRepository sources, CancellationToken cancellationToken) =>
{
    var sourceList = await sources.ListAsync(cancellationToken);
    return Results.Ok(new
    {
        totalTenders = await tenders.CountAsync(cancellationToken),
        totalSources = sourceList.Count,
        healthySources = sourceList.Count(x => x.Health == SourceHealth.Healthy),
        generatedAt = DateTimeOffset.UtcNow
    });
});

var workspace = app.MapGroup("/api/workspace");
workspace.MapGet("/{userKey}/items", async (string userKey, IWorkspaceRepository repository, CancellationToken cancellationToken) =>
    Results.Ok(await repository.ListItemsAsync(userKey, cancellationToken)));
workspace.MapPut("/{userKey}/items/{tenderId:guid}", async (string userKey, Guid tenderId, WorkspaceItemRequest request, IWorkspaceRepository repository, CancellationToken cancellationToken) =>
{
    var item = await repository.SaveItemAsync(userKey, tenderId, request.Stage, request.Notes, cancellationToken);
    await repository.SaveChangesAsync(cancellationToken);
    return Results.Ok(item);
});
workspace.MapDelete("/{userKey}/items/{tenderId:guid}", async (string userKey, Guid tenderId, IWorkspaceRepository repository, CancellationToken cancellationToken) =>
{
    await repository.RemoveItemAsync(userKey, tenderId, cancellationToken);
    await repository.SaveChangesAsync(cancellationToken);
    return Results.NoContent();
});
workspace.MapGet("/{userKey}/searches", async (string userKey, IWorkspaceRepository repository, CancellationToken cancellationToken) =>
    Results.Ok(await repository.ListSearchesAsync(userKey, cancellationToken)));
workspace.MapPost("/{userKey}/searches", async (string userKey, SavedSearchRequest request, IWorkspaceRepository repository, CancellationToken cancellationToken) =>
{
    var search = await repository.AddSearchAsync(new SavedSearch
    {
        UserKey = userKey,
        Name = request.Name,
        Query = request.Query,
        Country = request.Country,
        Category = request.Category
    }, cancellationToken);
    await repository.SaveChangesAsync(cancellationToken);
    return Results.Created($"/api/workspace/{userKey}/searches/{search.Id}", search);
});
workspace.MapDelete("/{userKey}/searches/{id:guid}", async (string userKey, Guid id, IWorkspaceRepository repository, CancellationToken cancellationToken) =>
{
    await repository.RemoveSearchAsync(userKey, id, cancellationToken);
    await repository.SaveChangesAsync(cancellationToken);
    return Results.NoContent();
});

await using (var scope = app.Services.CreateAsyncScope())
{
    var db = scope.ServiceProvider.GetRequiredService<TenderScopeDbContext>();
    await db.Database.EnsureCreatedAsync();

    var demo = new TenderSource { Key = "demo-open-source", Name = "TenderScope deterministic validation source", BaseUrl = new Uri("https://example.org/tenders"), Format = SourceFormat.Json, CountryCode = "INT" };
    var ted = new TenderSource { Key = "eu-ted-search", Name = "European Union Tenders Electronic Daily", BaseUrl = new Uri("https://api.ted.europa.eu/v3/notices/search"), Format = SourceFormat.Json, CountryCode = "EU" };
    ted.ConfigureInterval(360);

    var sources = scope.ServiceProvider.GetRequiredService<ITenderSourceRepository>();
    foreach (var seed in new[] { demo, ted })
        if (await sources.FindByKeyAsync(seed.Key, CancellationToken.None) is null)
            await sources.AddAsync(seed, CancellationToken.None);
    await sources.SaveChangesAsync(CancellationToken.None);
}

app.Run();

public sealed record WorkspaceItemRequest(OpportunityStage Stage, string? Notes);
public sealed record SavedSearchRequest(string Name, string? Query, string? Country, string? Category);

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
app.MapGet("/api/tenders", async (string? q, string? country, string? category, int? take, ITenderRepository repository, CancellationToken cancellationToken) =>
    Results.Ok(await repository.SearchAsync(q, country, category, take ?? 30, cancellationToken)));
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

await using (var scope = app.Services.CreateAsyncScope())
{
    var db = scope.ServiceProvider.GetRequiredService<TenderScopeDbContext>();
    await db.Database.EnsureCreatedAsync();

    var sources = scope.ServiceProvider.GetRequiredService<ITenderSourceRepository>();
    if (await sources.FindByKeyAsync("demo-open-source", CancellationToken.None) is null)
    {
        await sources.AddAsync(new TenderSource
        {
            Key = "demo-open-source",
            Name = "TenderScope deterministic validation source",
            BaseUrl = new Uri("https://example.org/tenders"),
            Format = SourceFormat.Json,
            CountryCode = "INT"
        }, CancellationToken.None);
        await sources.SaveChangesAsync(CancellationToken.None);
    }
}

app.Run();

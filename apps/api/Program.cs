using Microsoft.EntityFrameworkCore;
using TenderScope.Application.Contracts;
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
app.MapGet("/api/stats", async (ITenderRepository repository, CancellationToken cancellationToken) =>
    Results.Ok(new { totalTenders = await repository.CountAsync(cancellationToken), generatedAt = DateTimeOffset.UtcNow }));

await using (var scope = app.Services.CreateAsyncScope())
{
    var db = scope.ServiceProvider.GetRequiredService<TenderScopeDbContext>();
    await db.Database.EnsureCreatedAsync();
}

app.Run();

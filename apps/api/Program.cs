using System.Security.Cryptography;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using TenderScope.Api;
using TenderScope.Api.Auth;
using TenderScope.Application.Contracts;
using TenderScope.Domain.Entities;
using TenderScope.Infrastructure;
using TenderScope.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddTenderScopeAuth(builder.Configuration);
builder.Services.AddScoped<TenderIngestionService>();
builder.Services.AddHostedService<ScheduledIngestionWorker>();
builder.Services.AddOpenApi();
builder.Services.AddHealthChecks().AddDbContextCheck<TenderScopeDbContext>();
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("auth", context => RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 10, Window = TimeSpan.FromMinutes(1), QueueLimit = 0, AutoReplenishment = true }));
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
        RateLimitPartition.GetFixedWindowLimiter(context.Connection.RemoteIpAddress?.ToString() ?? "unknown", _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 120,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0,
            AutoReplenishment = true
        }));
});
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? ["http://localhost:3000"];
builder.Services.AddCors(options => options.AddDefaultPolicy(policy => policy.WithOrigins(allowedOrigins).AllowAnyHeader().AllowAnyMethod().AllowCredentials()));

var app = builder.Build();
app.Use(async (context, next) =>
{
    context.Response.Headers.XContentTypeOptions = "nosniff";
    context.Response.Headers.XFrameOptions = "DENY";
    context.Response.Headers.Append("Referrer-Policy", "strict-origin-when-cross-origin");
    context.Response.Headers.ContentSecurityPolicy = "default-src 'none'; frame-ancestors 'none'; base-uri 'none'";
    context.Response.Headers.Append("Permissions-Policy", "camera=(), microphone=(), geolocation=()");
    await next();
});
app.UseRateLimiter();
app.UseCors();
app.UseAuthentication();
app.UseAuthorization();
app.MapOpenApi();
app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false });
app.MapHealthChecks("/health/ready");
app.MapGet("/health", () => Results.Ok(new { status = "healthy", service = "tenderscope-api", version = "1.4.0", utc = DateTimeOffset.UtcNow }));
app.MapTenderScopeAuth();
app.MapOrganizationManagement();

app.MapGet("/api/tenders", async (string? q, string? country, string? category, DateTimeOffset? deadlineFrom, DateTimeOffset? deadlineTo, decimal? minValue, decimal? maxValue, string? sort, int? page, int? pageSize, ITenderRepository repository, CancellationToken cancellationToken) =>
    Results.Ok(await repository.SearchAdvancedAsync(q, country, category, deadlineFrom, deadlineTo, minValue, maxValue, sort ?? "published-desc", page ?? 1, pageSize ?? 30, cancellationToken)));
app.MapGet("/api/sources", async (ITenderSourceRepository repository, CancellationToken cancellationToken) => Results.Ok(await repository.ListAsync(cancellationToken)));
app.MapGet("/api/stats", async (ITenderRepository tenders, ITenderSourceRepository sources, CancellationToken cancellationToken) =>
{
    var sourceList = await sources.ListAsync(cancellationToken);
    return Results.Ok(new { totalTenders = await tenders.CountAsync(cancellationToken), totalSources = sourceList.Count, healthySources = sourceList.Count(x => x.Health == SourceHealth.Healthy), generatedAt = DateTimeOffset.UtcNow });
});

var workspace = app.MapGroup("/api/workspace");
workspace.AddEndpointFilter(async (context, next) =>
{
    var userKey = context.HttpContext.Request.RouteValues["userKey"]?.ToString();
    if (string.IsNullOrWhiteSpace(userKey) || userKey.Length is < 3 or > 120) return Results.BadRequest(new { error = "Invalid user key" });
    return await next(context);
});
workspace.MapGet("/{userKey}/items", async (string userKey, IWorkspaceRepository repository, CancellationToken cancellationToken) => Results.Ok(await repository.ListItemsAsync(userKey, cancellationToken)));
workspace.MapPut("/{userKey}/items/{tenderId:guid}", async (string userKey, Guid tenderId, WorkspaceItemRequest request, IWorkspaceRepository repository, CancellationToken cancellationToken) => { var item = await repository.SaveItemAsync(userKey, tenderId, request.Stage, request.Notes, cancellationToken); await repository.SaveChangesAsync(cancellationToken); return Results.Ok(item); });
workspace.MapDelete("/{userKey}/items/{tenderId:guid}", async (string userKey, Guid tenderId, IWorkspaceRepository repository, CancellationToken cancellationToken) => { await repository.RemoveItemAsync(userKey, tenderId, cancellationToken); await repository.SaveChangesAsync(cancellationToken); return Results.NoContent(); });
workspace.MapGet("/{userKey}/searches", async (string userKey, IWorkspaceRepository repository, CancellationToken cancellationToken) => Results.Ok(await repository.ListSearchesAsync(userKey, cancellationToken)));
workspace.MapPost("/{userKey}/searches", async (string userKey, SavedSearchRequest request, IWorkspaceRepository repository, CancellationToken cancellationToken) => { var search = await repository.AddSearchAsync(new SavedSearch { UserKey = userKey, Name = request.Name.Trim(), Query = request.Query, Country = request.Country, Category = request.Category }, cancellationToken); await repository.SaveChangesAsync(cancellationToken); return Results.Created($"/api/workspace/{userKey}/searches/{search.Id}", search); });
workspace.MapDelete("/{userKey}/searches/{id:guid}", async (string userKey, Guid id, IWorkspaceRepository repository, CancellationToken cancellationToken) => { await repository.RemoveSearchAsync(userKey, id, cancellationToken); await repository.SaveChangesAsync(cancellationToken); return Results.NoContent(); });

var admin = app.MapGroup("/api/admin").AddEndpointFilter(async (context, next) =>
{
    var configuredKey = builder.Configuration["Admin:ApiKey"];
    var suppliedKey = context.HttpContext.Request.Headers["X-Admin-Key"].ToString();
    if (string.IsNullOrWhiteSpace(configuredKey) || !CryptographicOperations.FixedTimeEquals(System.Text.Encoding.UTF8.GetBytes(configuredKey), System.Text.Encoding.UTF8.GetBytes(suppliedKey.PadRight(configuredKey.Length)[..configuredKey.Length]))) return Results.Unauthorized();
    return await next(context);
});
admin.MapGet("/audit", async (TenderScopeDbContext db, int? take, CancellationToken cancellationToken) => Results.Ok(await db.AuditLogs.AsNoTracking().OrderByDescending(x => x.CreatedAt).Take(Math.Clamp(take ?? 100, 1, 500)).ToListAsync(cancellationToken)));
admin.MapPost("/sync", async (TenderIngestionService ingestion, CancellationToken cancellationToken) => Results.Ok(await ingestion.RunAsync(cancellationToken)));
admin.MapPost("/sources/{id:guid}/enabled", async (Guid id, bool enabled, TenderScopeDbContext db, HttpContext http, CancellationToken cancellationToken) =>
{
    var source = await db.Sources.FindAsync([id], cancellationToken); if (source is null) return Results.NotFound();
    source.SetEnabled(enabled); db.AuditLogs.Add(new AuditLog { Action = enabled ? "source.enabled" : "source.disabled", Resource = $"source:{id}", ActorKey = "admin", IpAddress = http.Connection.RemoteIpAddress?.ToString() });
    await db.SaveChangesAsync(cancellationToken); return Results.Ok(source);
});

await using (var scope = app.Services.CreateAsyncScope())
{
    var db = scope.ServiceProvider.GetRequiredService<TenderScopeDbContext>();
    var connection = db.Database.GetDbConnection();
    await connection.OpenAsync();
    await using (var command = connection.CreateCommand())
    {
        command.CommandText = "SELECT to_regclass('public.tender_sources') IS NOT NULL";
        var sourcesTableExists = (bool)(await command.ExecuteScalarAsync() ?? false);
        if (!sourcesTableExists)
        {
            var databaseCreator = db.GetService<IRelationalDatabaseCreator>();
            await databaseCreator.CreateTablesAsync();
        }
    }
    await connection.CloseAsync();
    await db.EnsureIdentitySchemaAsync();

    var demo = new TenderSource { Key = "demo-open-source", Name = "TenderScope deterministic validation source", BaseUrl = new Uri("https://example.org/tenders"), Format = SourceFormat.Json, CountryCode = "INT" };
    var ted = new TenderSource { Key = "eu-ted-search", Name = "European Union Tenders Electronic Daily", BaseUrl = new Uri("https://api.ted.europa.eu/v3/notices/search"), Format = SourceFormat.Json, CountryCode = "EU" }; ted.ConfigureInterval(360);
    var sources = scope.ServiceProvider.GetRequiredService<ITenderSourceRepository>();
    foreach (var seed in new[] { demo, ted }) if (await sources.FindByKeyAsync(seed.Key, CancellationToken.None) is null) await sources.AddAsync(seed, CancellationToken.None);
    await sources.SaveChangesAsync(CancellationToken.None);
}

app.Run();

public sealed record WorkspaceItemRequest(OpportunityStage Stage, string? Notes);
public sealed record SavedSearchRequest(string Name, string? Query, string? Country, string? Category);

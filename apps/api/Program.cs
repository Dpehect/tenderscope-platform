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
builder.Services.AddApiHardening();
builder.Services.AddProductionOperations();
builder.Services.AddScoped<TenderIngestionService>();
builder.Services.AddHostedService<ScheduledIngestionWorker>();
builder.Services.AddHostedService<WatchlistMatchingWorker>();
builder.Services.AddHostedService<MaintenanceWorker>();
builder.Services.AddOpenApi();
builder.Services.AddHealthChecks().AddDbContextCheck<TenderScopeDbContext>();
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("auth", context => RateLimitPartition.GetFixedWindowLimiter(context.Connection.RemoteIpAddress?.ToString() ?? "unknown", _ => new FixedWindowRateLimiterOptions { PermitLimit = 10, Window = TimeSpan.FromMinutes(1), QueueLimit = 0, AutoReplenishment = true }));
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context => RateLimitPartition.GetFixedWindowLimiter(context.Connection.RemoteIpAddress?.ToString() ?? "unknown", _ => new FixedWindowRateLimiterOptions { PermitLimit = 120, Window = TimeSpan.FromMinutes(1), QueueLimit = 0, AutoReplenishment = true }));
});
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? ["http://localhost:3000"];
builder.Services.AddCors(options => options.AddDefaultPolicy(policy => policy.WithOrigins(allowedOrigins).AllowAnyHeader().AllowAnyMethod().AllowCredentials().SetPreflightMaxAge(TimeSpan.FromHours(1))));

var app = builder.Build();
app.UseApiHardening();
app.UseOperationalMetrics();
app.UseHttpsRedirection();
app.UseRateLimiter();
app.UseCors();
app.UseAuthentication();
app.UseAuthorization();
app.MapOpenApi();
app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false });
app.MapHealthChecks("/health/ready");
app.MapGet("/health", () => Results.Ok(new { status = "healthy", service = "tenderscope-api", version = "3.2.0", utc = DateTimeOffset.UtcNow }));
app.MapTenderScopeAuth();
app.MapAccountRecovery();
app.MapOrganizationManagement();
app.MapTenantWorkspace();
app.MapWatchlists();
app.MapNotifications();
app.MapOrganizationAnalytics();
app.MapIntelligence();
app.MapAdminOperations();
app.MapSecurityManagement();
app.MapOperationalMetrics();
app.MapGet("/api/tenders", async (string? q, string? country, string? category, DateTimeOffset? deadlineFrom, DateTimeOffset? deadlineTo, decimal? minValue, decimal? maxValue, string? sort, int? page, int? pageSize, ITenderRepository repository, CancellationToken ct) => Results.Ok(await repository.SearchAdvancedAsync(q, country, category, deadlineFrom, deadlineTo, minValue, maxValue, sort ?? "published-desc", page ?? 1, pageSize ?? 30, ct)));
app.MapGet("/api/sources", async (ITenderSourceRepository repository, CancellationToken ct) => Results.Ok(await repository.ListAsync(ct)));
app.MapGet("/api/stats", async (ITenderRepository tenders, ITenderSourceRepository sources, CancellationToken ct) => { var sourceList = await sources.ListAsync(ct); return Results.Ok(new { totalTenders = await tenders.CountAsync(ct), totalSources = sourceList.Count, healthySources = sourceList.Count(x => x.Health == SourceHealth.Healthy), generatedAt = DateTimeOffset.UtcNow }); });
var admin = app.MapGroup("/api/admin-key").AddEndpointFilter(async (context, next) => { var configuredKey = builder.Configuration["Admin:ApiKey"]; var suppliedKey = context.HttpContext.Request.Headers["X-Admin-Key"].ToString(); if (string.IsNullOrWhiteSpace(configuredKey)) return Results.Unauthorized(); var expected = System.Text.Encoding.UTF8.GetBytes(configuredKey); var actual = System.Text.Encoding.UTF8.GetBytes(suppliedKey.PadRight(configuredKey.Length)[..configuredKey.Length]); return CryptographicOperations.FixedTimeEquals(expected, actual) ? await next(context) : Results.Unauthorized(); });
admin.MapGet("/audit", async (TenderScopeDbContext db, int? take, CancellationToken ct) => Results.Ok(await db.AuditLogs.AsNoTracking().OrderByDescending(x => x.CreatedAt).Take(Math.Clamp(take ?? 100, 1, 500)).ToListAsync(ct)));
admin.MapPost("/sync", async (TenderIngestionService ingestion, CancellationToken ct) => Results.Ok(await ingestion.RunAsync(ct)));

await using (var scope = app.Services.CreateAsyncScope())
{
    var db = scope.ServiceProvider.GetRequiredService<TenderScopeDbContext>();
    var connection = db.Database.GetDbConnection();
    await connection.OpenAsync();
    await using (var command = connection.CreateCommand()) { command.CommandText = "SELECT to_regclass('public.tender_sources') IS NOT NULL"; var exists = (bool)(await command.ExecuteScalarAsync() ?? false); if (!exists) await db.GetService<IRelationalDatabaseCreator>().CreateTablesAsync(); }
    await connection.CloseAsync();
    await db.EnsureIdentitySchemaAsync();
    await db.EnsureAccountRecoverySchemaAsync();
    await db.EnsureWorkspaceTenantSchemaAsync();
    await db.EnsureNotificationSchemaAsync();
    await db.ApplyProductionMigrationsAsync();
    var demo = new TenderSource { Key = "demo-open-source", Name = "TenderScope deterministic validation source", BaseUrl = new Uri("https://example.org/tenders"), Format = SourceFormat.Json, CountryCode = "INT" };
    var ted = new TenderSource { Key = "eu-ted-search", Name = "European Union Tenders Electronic Daily", BaseUrl = new Uri("https://api.ted.europa.eu/v3/notices/search"), Format = SourceFormat.Json, CountryCode = "EU" }; ted.ConfigureInterval(360);
    var sources = scope.ServiceProvider.GetRequiredService<ITenderSourceRepository>();
    foreach (var seed in new[] { demo, ted }) if (await sources.FindByKeyAsync(seed.Key, CancellationToken.None) is null) await sources.AddAsync(seed, CancellationToken.None);
    await sources.SaveChangesAsync(CancellationToken.None);
}
app.Run();

public partial class Program;

using System.Data;
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using TenderScope.Infrastructure.Persistence;

namespace TenderScope.Api.Auth;

public static class CrawlerOperationsModule
{
    public static IEndpointRouteBuilder MapCrawlerOperations(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/admin/v2/crawler").RequireAuthorization();
        group.AddEndpointFilter(async (context, next) =>
        {
            var user = context.HttpContext.User;
            return user.IsInRole("Admin") || user.IsInRole("Owner") ? await next(context) : Results.Forbid();
        });

        group.MapGet("/runs", async (Guid? sourceId, int? take, TenderScopeDbContext db, CancellationToken ct) =>
        {
            var rows = await ReadRowsAsync(db, """
SELECT "Id", "SourceId", "SourceKey", "ParserVersion", "Status", "Attempt", "FetchedCount", "ImportedCount", "RejectedCount", "DurationMilliseconds", "Error", "StartedAt", "CompletedAt"
FROM crawl_runs
WHERE (@source_id IS NULL OR "SourceId" = @source_id)
ORDER BY "StartedAt" DESC
LIMIT @take
""", sourceId, Math.Clamp(take ?? 100, 1, 500), ct);
            return Results.Ok(rows);
        });

        group.MapGet("/dead-letters", async (Guid? sourceId, bool? unresolvedOnly, int? take, TenderScopeDbContext db, CancellationToken ct) =>
        {
            var rows = await ReadRowsAsync(db, """
SELECT "Id", "CrawlRunId", "SourceId", "SourceKey", "ExternalId", "Error", "PayloadPreview", "Attempts", "CreatedAt", "ResolvedAt"
FROM crawl_dead_letters
WHERE (@source_id IS NULL OR "SourceId" = @source_id)
  AND (@unresolved = false OR "ResolvedAt" IS NULL)
ORDER BY "CreatedAt" DESC
LIMIT @take
""", sourceId, Math.Clamp(take ?? 100, 1, 500), ct, unresolvedOnly ?? true);
            return Results.Ok(rows);
        });

        group.MapPatch("/dead-letters/{id:guid}/resolve", async (Guid id, ClaimsPrincipal principal, TenderScopeDbContext db, CancellationToken ct) =>
        {
            var affected = await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE crawl_dead_letters SET \"ResolvedAt\"={DateTimeOffset.UtcNow} WHERE \"Id\"={id} AND \"ResolvedAt\" IS NULL", ct);
            if (affected == 0) return Results.NotFound();
            db.AuditLogs.Add(new Domain.Entities.AuditLog { Action = "crawler.dead_letter.resolved", Resource = $"dead-letter:{id}", ActorKey = principal.FindFirstValue(ClaimTypes.NameIdentifier) ?? "admin" });
            await db.SaveChangesAsync(ct);
            return Results.NoContent();
        });

        group.MapPost("/sources/{id:guid}/run", async (Guid id, TenderScopeDbContext db, TenderIngestionService ingestion, CancellationToken ct) =>
        {
            var source = await db.Sources.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct);
            return source is null ? Results.NotFound() : Results.Ok(await ingestion.RunAsync(ct, source.Key));
        });

        return endpoints;
    }

    private static async Task<List<Dictionary<string, object?>>> ReadRowsAsync(TenderScopeDbContext db, string sql, Guid? sourceId, int take, CancellationToken ct, bool unresolved = false)
    {
        var connection = db.Database.GetDbConnection();
        await connection.OpenAsync(ct);
        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = sql;
            var sourceParameter = command.CreateParameter(); sourceParameter.ParameterName = "source_id"; sourceParameter.Value = sourceId.HasValue ? sourceId.Value : DBNull.Value; command.Parameters.Add(sourceParameter);
            var takeParameter = command.CreateParameter(); takeParameter.ParameterName = "take"; takeParameter.Value = take; command.Parameters.Add(takeParameter);
            if (sql.Contains("@unresolved", StringComparison.Ordinal)) { var unresolvedParameter = command.CreateParameter(); unresolvedParameter.ParameterName = "unresolved"; unresolvedParameter.Value = unresolved; command.Parameters.Add(unresolvedParameter); }
            await using var reader = await command.ExecuteReaderAsync(ct);
            var rows = new List<Dictionary<string, object?>>();
            while (await reader.ReadAsync(ct))
            {
                var row = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
                for (var i = 0; i < reader.FieldCount; i++) row[reader.GetName(i)] = await reader.IsDBNullAsync(i, ct) ? null : reader.GetValue(i);
                rows.Add(row);
            }
            return rows;
        }
        finally { await connection.CloseAsync(); }
    }
}

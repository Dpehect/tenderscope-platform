using System.Security.Claims;
using System.Text;
using Microsoft.EntityFrameworkCore;
using TenderScope.Domain.Entities;
using TenderScope.Infrastructure.Persistence;

namespace TenderScope.Api.Auth;

public static class AdminOperationsModule
{
    public static IEndpointRouteBuilder MapAdminOperations(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/admin/v2").RequireAuthorization();

        group.AddEndpointFilter(async (context, next) =>
        {
            var user = context.HttpContext.User;
            return user.IsInRole("Admin") || user.IsInRole("Owner") ? await next(context) : Results.Forbid();
        });

        group.MapGet("/overview", async (TenderScopeDbContext db, CancellationToken ct) =>
        {
            var now = DateTimeOffset.UtcNow;
            return Results.Ok(new
            {
                users = await db.Users.CountAsync(ct),
                organizations = await db.Organizations.CountAsync(ct),
                tenders = await db.Tenders.CountAsync(ct),
                sources = await db.Sources.CountAsync(ct),
                healthySources = await db.Sources.CountAsync(x => x.Health == SourceHealth.Healthy, ct),
                failingSources = await db.Sources.CountAsync(x => x.Health == SourceHealth.Failing, ct),
                unreadNotifications = await db.Notifications.CountAsync(x => x.ReadAt == null, ct),
                activeRefreshTokens = await db.RefreshTokens.CountAsync(x => x.RevokedAt == null && x.ExpiresAt > now, ct),
                generatedAt = now
            });
        });

        group.MapGet("/sources", async (TenderScopeDbContext db, CancellationToken ct) =>
            Results.Ok(await db.Sources.AsNoTracking().OrderBy(x => x.Name).ToListAsync(ct)));

        group.MapPatch("/sources/{id:guid}", async (Guid id, SourceAdminRequest request, ClaimsPrincipal principal, TenderScopeDbContext db, CancellationToken ct) =>
        {
            var source = await db.Sources.SingleOrDefaultAsync(x => x.Id == id, ct);
            if (source is null) return Results.NotFound();
            source.SetEnabled(request.Enabled);
            if (request.IntervalMinutes.HasValue) source.ConfigureInterval(Math.Clamp(request.IntervalMinutes.Value, 15, 10080));
            db.AuditLogs.Add(new AuditLog { Action = "source.configuration.updated", Resource = $"source:{id}", ActorKey = principal.FindFirstValue(ClaimTypes.NameIdentifier) ?? "admin", Detail = $"enabled={request.Enabled};interval={request.IntervalMinutes}" });
            await db.SaveChangesAsync(ct);
            return Results.Ok(source);
        });

        group.MapPost("/sources/{id:guid}/run", async (Guid id, ClaimsPrincipal principal, TenderScopeDbContext db, TenderIngestionService ingestion, CancellationToken ct) =>
        {
            if (!await db.Sources.AnyAsync(x => x.Id == id, ct)) return Results.NotFound();
            var report = await ingestion.RunAsync(ct);
            db.AuditLogs.Add(new AuditLog { Action = "ingestion.manual.completed", Resource = $"source:{id}", ActorKey = principal.FindFirstValue(ClaimTypes.NameIdentifier) ?? "admin", Detail = $"imported={report.Imported};started={report.StartedAt:O};completed={report.CompletedAt:O}" });
            await db.SaveChangesAsync(ct);
            return Results.Ok(report);
        });

        group.MapGet("/organizations", async (TenderScopeDbContext db, CancellationToken ct) => Results.Ok(await db.Organizations.AsNoTracking()
            .Select(x => new { x.Id, x.Name, x.Slug, x.IsActive, x.CreatedAt, members = db.OrganizationMemberships.Count(m => m.OrganizationId == x.Id), workspaceItems = db.WorkspaceItems.Count(w => w.OrganizationId == x.Id) })
            .OrderByDescending(x => x.CreatedAt).Take(500).ToListAsync(ct)));

        group.MapGet("/users", async (TenderScopeDbContext db, CancellationToken ct) => Results.Ok(await db.Users.AsNoTracking()
            .Select(x => new { x.Id, x.Email, x.DisplayName, x.IsActive, x.CreatedAt, x.LastLoginAt, organizations = db.OrganizationMemberships.Count(m => m.UserId == x.Id) })
            .OrderByDescending(x => x.CreatedAt).Take(1000).ToListAsync(ct)));

        group.MapGet("/audit", async (int? take, TenderScopeDbContext db, CancellationToken ct) => Results.Ok(await db.AuditLogs.AsNoTracking()
            .OrderByDescending(x => x.CreatedAt).Take(Math.Clamp(take ?? 200, 1, 1000)).ToListAsync(ct)));

        group.MapGet("/reports/{format}", async (string format, TenderScopeDbContext db, CancellationToken ct) =>
        {
            var rows = await db.Tenders.AsNoTracking().OrderByDescending(x => x.PublishedAt).Take(5000)
                .Select(x => new { x.Id, x.Title, x.BuyerName, x.CountryCode, x.Category, x.EstimatedValue, x.Currency, x.PublishedAt, x.DeadlineAt, SourceUrl = x.SourceUrl.ToString() }).ToListAsync(ct);
            var normalized = format.ToLowerInvariant();
            if (normalized == "csv") return Results.File(Encoding.UTF8.GetBytes(ToDelimited(rows, ',')), "text/csv; charset=utf-8", $"tenderscope-report-{DateTime.UtcNow:yyyyMMdd}.csv");
            if (normalized is "xlsx" or "excel") return Results.File(Encoding.UTF8.GetBytes(ToDelimited(rows, '\t')), "application/vnd.ms-excel", $"tenderscope-report-{DateTime.UtcNow:yyyyMMdd}.xls");
            if (normalized == "pdf") return Results.File(BuildPdf(rows.Count, rows.Sum(x => x.EstimatedValue ?? 0), rows.GroupBy(x => x.CountryCode).OrderByDescending(x => x.Count()).Take(10).Select(x => $"{x.Key}: {x.Count()}").ToArray()), "application/pdf", $"tenderscope-report-{DateTime.UtcNow:yyyyMMdd}.pdf");
            return Results.BadRequest(new { error = "Supported formats: csv, excel, pdf." });
        });

        return endpoints;
    }

    private static string ToDelimited<T>(IReadOnlyList<T> rows, char separator)
    {
        var props = typeof(T).GetProperties();
        var builder = new StringBuilder();
        builder.AppendLine(string.Join(separator, props.Select(x => Escape(x.Name, separator))));
        foreach (var row in rows) builder.AppendLine(string.Join(separator, props.Select(x => Escape(x.GetValue(row)?.ToString() ?? string.Empty, separator))));
        return builder.ToString();
    }

    private static string Escape(string value, char separator) => $"\"{value.Replace("\"", "\"\"")}\"";

    private static byte[] BuildPdf(int count, decimal value, string[] countries)
    {
        var text = $"TenderScope Executive Report\\nGenerated: {DateTimeOffset.UtcNow:u}\\nIndexed opportunities: {count}\\nDisclosed value: {value:N0}\\nTop markets: {string.Join(", ", countries)}";
        var escaped = text.Replace("\\", "\\\\").Replace("(", "\\(").Replace(")", "\\)").Replace("\\n", ") Tj 0 -18 Td (");
        var stream = $"BT /F1 12 Tf 50 760 Td ({escaped}) Tj ET";
        var objects = new[] { "1 0 obj << /Type /Catalog /Pages 2 0 R >> endobj\n", "2 0 obj << /Type /Pages /Kids [3 0 R] /Count 1 >> endobj\n", "3 0 obj << /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Resources << /Font << /F1 5 0 R >> >> /Contents 4 0 R >> endobj\n", $"4 0 obj << /Length {stream.Length} >> stream\n{stream}\nendstream endobj\n", "5 0 obj << /Type /Font /Subtype /Type1 /BaseFont /Helvetica >> endobj\n" };
        var output = new StringBuilder("%PDF-1.4\n"); var offsets = new List<int> { 0 };
        foreach (var item in objects) { offsets.Add(Encoding.ASCII.GetByteCount(output.ToString())); output.Append(item); }
        var xref = Encoding.ASCII.GetByteCount(output.ToString()); output.Append($"xref\n0 {objects.Length + 1}\n0000000000 65535 f \n");
        foreach (var offset in offsets.Skip(1)) output.Append($"{offset:0000000000} 00000 n \n");
        output.Append($"trailer << /Size {objects.Length + 1} /Root 1 0 R >>\nstartxref\n{xref}\n%%EOF");
        return Encoding.ASCII.GetBytes(output.ToString());
    }
}

public sealed record SourceAdminRequest(bool Enabled, int? IntervalMinutes);

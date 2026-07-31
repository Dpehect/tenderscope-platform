using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using TenderScope.Domain.Entities;
using TenderScope.Infrastructure.Persistence;

namespace TenderScope.Api.Auth;

public static class WatchlistModule
{
    public static IEndpointRouteBuilder MapWatchlists(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/workspace/v2/watchlists").RequireAuthorization();

        group.MapGet("/", async (ClaimsPrincipal principal, TenderScopeDbContext db, CancellationToken ct) =>
        {
            var tenant = TenantContext.From(principal);
            if (tenant is null) return Results.Unauthorized();
            var rows = await db.SavedSearches.AsNoTracking()
                .Where(x => x.OrganizationId == tenant.OrganizationId)
                .OrderByDescending(x => x.CreatedAt)
                .ToListAsync(ct);
            return Results.Ok(rows);
        });

        group.MapPost("/", async (WatchlistRequest request, ClaimsPrincipal principal, TenderScopeDbContext db, HttpContext http, CancellationToken ct) =>
        {
            var tenant = TenantContext.From(principal);
            if (tenant is null) return Results.Unauthorized();
            var validation = Validate(request);
            if (validation is not null) return validation;

            var entity = SavedSearch.Create(tenant.OrganizationId, tenant.UserId, request.Name, request.Query, request.Country, request.Category, request.NotificationsEnabled);
            db.SavedSearches.Add(entity);
            AddAudit(db, tenant, http, "workspace.watchlist.created", $"watchlist:{entity.Id}");
            await db.SaveChangesAsync(ct);
            return Results.Created($"/api/workspace/v2/watchlists/{entity.Id}", entity);
        });

        group.MapPut("/{id:guid}", async (Guid id, WatchlistRequest request, ClaimsPrincipal principal, TenderScopeDbContext db, HttpContext http, CancellationToken ct) =>
        {
            var tenant = TenantContext.From(principal);
            if (tenant is null) return Results.Unauthorized();
            var validation = Validate(request);
            if (validation is not null) return validation;

            var entity = await db.SavedSearches.SingleOrDefaultAsync(x => x.Id == id && x.OrganizationId == tenant.OrganizationId, ct);
            if (entity is null) return Results.NotFound();
            entity.Update(request.Name, request.Query, request.Country, request.Category, request.NotificationsEnabled);
            AddAudit(db, tenant, http, "workspace.watchlist.updated", $"watchlist:{id}");
            await db.SaveChangesAsync(ct);
            return Results.Ok(entity);
        });

        group.MapPatch("/{id:guid}/notifications", async (Guid id, NotificationPreferenceRequest request, ClaimsPrincipal principal, TenderScopeDbContext db, HttpContext http, CancellationToken ct) =>
        {
            var tenant = TenantContext.From(principal);
            if (tenant is null) return Results.Unauthorized();
            var entity = await db.SavedSearches.SingleOrDefaultAsync(x => x.Id == id && x.OrganizationId == tenant.OrganizationId, ct);
            if (entity is null) return Results.NotFound();
            entity.SetNotifications(request.Enabled);
            AddAudit(db, tenant, http, request.Enabled ? "workspace.watchlist.notifications_enabled" : "workspace.watchlist.notifications_disabled", $"watchlist:{id}");
            await db.SaveChangesAsync(ct);
            return Results.Ok(entity);
        });

        group.MapDelete("/{id:guid}", async (Guid id, ClaimsPrincipal principal, TenderScopeDbContext db, HttpContext http, CancellationToken ct) =>
        {
            var tenant = TenantContext.From(principal);
            if (tenant is null) return Results.Unauthorized();
            var entity = await db.SavedSearches.SingleOrDefaultAsync(x => x.Id == id && x.OrganizationId == tenant.OrganizationId, ct);
            if (entity is null) return Results.NotFound();
            db.SavedSearches.Remove(entity);
            AddAudit(db, tenant, http, "workspace.watchlist.removed", $"watchlist:{id}");
            await db.SaveChangesAsync(ct);
            return Results.NoContent();
        });

        return endpoints;
    }

    private static IResult? Validate(WatchlistRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name) || request.Name.Trim().Length > 180)
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["name"] = ["Name is required and must not exceed 180 characters."] });
        if (!string.IsNullOrWhiteSpace(request.Country) && request.Country.Trim().Length > 3)
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["country"] = ["Country code must not exceed 3 characters."] });
        return null;
    }

    private static void AddAudit(TenderScopeDbContext db, TenantContext tenant, HttpContext http, string action, string resource) =>
        db.AuditLogs.Add(new AuditLog { Action = action, Resource = resource, ActorKey = tenant.UserId.ToString(), Detail = $"organization={tenant.OrganizationId}", IpAddress = http.Connection.RemoteIpAddress?.ToString() });

    private sealed record TenantContext(Guid UserId, Guid OrganizationId)
    {
        public static TenantContext? From(ClaimsPrincipal principal) =>
            Guid.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out var userId) && Guid.TryParse(principal.FindFirstValue("organization_id"), out var organizationId)
                ? new TenantContext(userId, organizationId) : null;
    }
}

public sealed record WatchlistRequest(string Name, string? Query, string? Country, string? Category, bool NotificationsEnabled);
public sealed record NotificationPreferenceRequest(bool Enabled);

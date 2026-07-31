using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using TenderScope.Application.Contracts;
using TenderScope.Domain.Entities;
using TenderScope.Infrastructure.Persistence;

namespace TenderScope.Api.Auth;

public static class WorkspaceModule
{
    public static IEndpointRouteBuilder MapTenantWorkspace(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/workspace/v2").RequireAuthorization();

        group.MapGet("/items", async (ClaimsPrincipal principal, IWorkspaceRepository repository, CancellationToken ct) =>
        {
            var tenant = TenantContext.From(principal);
            return tenant is null ? Results.Unauthorized() : Results.Ok(await repository.ListItemsAsync(tenant.OrganizationId, ct));
        });

        group.MapPut("/items/{tenderId:guid}", async (Guid tenderId, WorkspaceItemRequestV2 request, ClaimsPrincipal principal, IWorkspaceRepository repository, TenderScopeDbContext db, HttpContext http, CancellationToken ct) =>
        {
            var tenant = TenantContext.From(principal);
            if (tenant is null) return Results.Unauthorized();
            if (request.AssigneeUserId.HasValue && !await IsOrganizationMemberAsync(db, tenant.OrganizationId, request.AssigneeUserId.Value, ct))
                return Results.BadRequest(new { error = "Assignee must be an active organization member." });

            var item = await repository.SaveItemAsync(tenant.OrganizationId, tenant.UserId, tenderId, request.Stage, request.Notes, ct);
            item.Configure(request.Notes, request.Tags, request.InternalDeadline, request.AssigneeUserId);
            AddActivity(db, tenant, item.Id, "workspace.item.saved", $"stage={request.Stage};assignee={request.AssigneeUserId}");
            AddAudit(db, tenant, http, "workspace.item.saved", $"tender:{tenderId}", $"stage={request.Stage}");
            await repository.SaveChangesAsync(ct);
            return Results.Ok(item);
        });

        group.MapPatch("/items/{id:guid}/move", async (Guid id, MoveWorkspaceItemRequest request, ClaimsPrincipal principal, TenderScopeDbContext db, HttpContext http, CancellationToken ct) =>
        {
            var tenant = TenantContext.From(principal);
            if (tenant is null) return Results.Unauthorized();
            var item = await db.WorkspaceItems.SingleOrDefaultAsync(x => x.Id == id && x.OrganizationId == tenant.OrganizationId, ct);
            if (item is null) return Results.NotFound();
            var previousStage = item.Stage;
            item.Move(request.Stage, request.Position);
            AddActivity(db, tenant, item.Id, "workspace.item.moved", $"from={previousStage};to={request.Stage};position={request.Position}");
            AddAudit(db, tenant, http, "workspace.item.moved", $"workspace-item:{id}", $"stage={request.Stage};position={request.Position}");
            await db.SaveChangesAsync(ct);
            return Results.Ok(item);
        });

        group.MapPatch("/items/{id:guid}/details", async (Guid id, UpdateWorkspaceDetailsRequest request, ClaimsPrincipal principal, TenderScopeDbContext db, HttpContext http, CancellationToken ct) =>
        {
            var tenant = TenantContext.From(principal);
            if (tenant is null) return Results.Unauthorized();
            if (request.AssigneeUserId.HasValue && !await IsOrganizationMemberAsync(db, tenant.OrganizationId, request.AssigneeUserId.Value, ct))
                return Results.BadRequest(new { error = "Assignee must be an active organization member." });
            if (request.InternalDeadline.HasValue && request.InternalDeadline.Value < DateTimeOffset.UtcNow.AddYears(-1))
                return Results.BadRequest(new { error = "Internal deadline is invalid." });

            var item = await db.WorkspaceItems.SingleOrDefaultAsync(x => x.Id == id && x.OrganizationId == tenant.OrganizationId, ct);
            if (item is null) return Results.NotFound();
            item.Configure(request.Notes, request.Tags, request.InternalDeadline, request.AssigneeUserId);
            AddActivity(db, tenant, item.Id, "workspace.item.details_updated", $"assignee={request.AssigneeUserId};deadline={request.InternalDeadline:O}");
            AddAudit(db, tenant, http, "workspace.item.details_updated", $"workspace-item:{id}", null);
            await db.SaveChangesAsync(ct);
            return Results.Ok(item);
        });

        group.MapGet("/items/{id:guid}/activities", async (Guid id, int? take, ClaimsPrincipal principal, TenderScopeDbContext db, CancellationToken ct) =>
        {
            var tenant = TenantContext.From(principal);
            if (tenant is null) return Results.Unauthorized();
            if (!await db.WorkspaceItems.AnyAsync(x => x.Id == id && x.OrganizationId == tenant.OrganizationId, ct)) return Results.NotFound();
            var limit = Math.Clamp(take ?? 100, 1, 300);
            var activities = await (from activity in db.WorkspaceActivities.AsNoTracking()
                                    join user in db.Users.AsNoTracking() on activity.ActorUserId equals user.Id
                                    where activity.OrganizationId == tenant.OrganizationId && activity.WorkspaceItemId == id
                                    orderby activity.CreatedAt descending
                                    select new { activity.Id, activity.Action, activity.Detail, activity.CreatedAt, activity.ActorUserId, user.DisplayName })
                .Take(limit).ToListAsync(ct);
            return Results.Ok(activities);
        });

        group.MapGet("/activities", async (int? take, ClaimsPrincipal principal, TenderScopeDbContext db, CancellationToken ct) =>
        {
            var tenant = TenantContext.From(principal);
            if (tenant is null) return Results.Unauthorized();
            var limit = Math.Clamp(take ?? 100, 1, 300);
            var activities = await (from activity in db.WorkspaceActivities.AsNoTracking()
                                    join user in db.Users.AsNoTracking() on activity.ActorUserId equals user.Id
                                    where activity.OrganizationId == tenant.OrganizationId
                                    orderby activity.CreatedAt descending
                                    select new { activity.Id, activity.WorkspaceItemId, activity.Action, activity.Detail, activity.CreatedAt, activity.ActorUserId, user.DisplayName })
                .Take(limit).ToListAsync(ct);
            return Results.Ok(activities);
        });

        group.MapDelete("/items/{tenderId:guid}", async (Guid tenderId, ClaimsPrincipal principal, IWorkspaceRepository repository, TenderScopeDbContext db, HttpContext http, CancellationToken ct) =>
        {
            var tenant = TenantContext.From(principal);
            if (tenant is null) return Results.Unauthorized();
            var item = await db.WorkspaceItems.AsNoTracking().SingleOrDefaultAsync(x => x.OrganizationId == tenant.OrganizationId && x.TenderId == tenderId, ct);
            if (item is not null) AddAudit(db, tenant, http, "workspace.item.removed", $"tender:{tenderId}", null);
            await repository.RemoveItemAsync(tenant.OrganizationId, tenderId, ct);
            await repository.SaveChangesAsync(ct);
            return Results.NoContent();
        });

        group.MapGet("/searches", async (ClaimsPrincipal principal, IWorkspaceRepository repository, CancellationToken ct) =>
        {
            var tenant = TenantContext.From(principal);
            return tenant is null ? Results.Unauthorized() : Results.Ok(await repository.ListSearchesAsync(tenant.OrganizationId, ct));
        });

        group.MapPost("/searches", async (SavedSearchRequestV2 request, ClaimsPrincipal principal, IWorkspaceRepository repository, TenderScopeDbContext db, HttpContext http, CancellationToken ct) =>
        {
            var tenant = TenantContext.From(principal);
            if (tenant is null) return Results.Unauthorized();
            if (string.IsNullOrWhiteSpace(request.Name) || request.Name.Trim().Length > 180)
                return Results.ValidationProblem(new Dictionary<string, string[]> { ["name"] = ["Name is required and must not exceed 180 characters."] });
            var search = new SavedSearch { OrganizationId = tenant.OrganizationId, CreatedByUserId = tenant.UserId, Name = request.Name.Trim(), Query = request.Query, Country = request.Country, Category = request.Category };
            search.SetNotifications(request.NotificationsEnabled ?? true);
            var saved = await repository.AddSearchAsync(tenant.OrganizationId, tenant.UserId, search, ct);
            AddAudit(db, tenant, http, "workspace.search.created", $"search:{saved.Id}", null);
            await repository.SaveChangesAsync(ct);
            return Results.Created($"/api/workspace/v2/searches/{saved.Id}", saved);
        });

        group.MapDelete("/searches/{id:guid}", async (Guid id, ClaimsPrincipal principal, IWorkspaceRepository repository, TenderScopeDbContext db, HttpContext http, CancellationToken ct) =>
        {
            var tenant = TenantContext.From(principal);
            if (tenant is null) return Results.Unauthorized();
            await repository.RemoveSearchAsync(tenant.OrganizationId, id, ct);
            AddAudit(db, tenant, http, "workspace.search.removed", $"search:{id}", null);
            await repository.SaveChangesAsync(ct);
            return Results.NoContent();
        });

        return endpoints;
    }

    private static Task<bool> IsOrganizationMemberAsync(TenderScopeDbContext db, Guid organizationId, Guid userId, CancellationToken ct) =>
        db.OrganizationMemberships.AnyAsync(x => x.OrganizationId == organizationId && x.UserId == userId, ct);

    private static void AddActivity(TenderScopeDbContext db, TenantContext tenant, Guid workspaceItemId, string action, string? detail) =>
        db.WorkspaceActivities.Add(new WorkspaceActivity { OrganizationId = tenant.OrganizationId, WorkspaceItemId = workspaceItemId, ActorUserId = tenant.UserId, Action = action, Detail = detail });

    private static void AddAudit(TenderScopeDbContext db, TenantContext tenant, HttpContext http, string action, string resource, string? detail) =>
        db.AuditLogs.Add(new AuditLog { Action = action, Resource = resource, ActorKey = tenant.UserId.ToString(), Detail = $"organization={tenant.OrganizationId};{detail}", IpAddress = http.Connection.RemoteIpAddress?.ToString() });

    private sealed record TenantContext(Guid UserId, Guid OrganizationId)
    {
        public static TenantContext? From(ClaimsPrincipal principal) =>
            Guid.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out var userId) && Guid.TryParse(principal.FindFirstValue("organization_id"), out var organizationId)
                ? new TenantContext(userId, organizationId) : null;
    }
}

public sealed record WorkspaceItemRequestV2(OpportunityStage Stage, string? Notes, string[]? Tags, DateTimeOffset? InternalDeadline, Guid? AssigneeUserId);
public sealed record MoveWorkspaceItemRequest(OpportunityStage Stage, decimal Position);
public sealed record UpdateWorkspaceDetailsRequest(string? Notes, string[]? Tags, DateTimeOffset? InternalDeadline, Guid? AssigneeUserId);
public sealed record SavedSearchRequestV2(string Name, string? Query, string? Country, string? Category, bool? NotificationsEnabled);

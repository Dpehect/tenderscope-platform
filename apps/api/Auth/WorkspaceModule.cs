using System.Security.Claims;
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
            var item = await repository.SaveItemAsync(tenant.OrganizationId, tenant.UserId, tenderId, request.Stage, request.Notes, ct);
            db.AuditLogs.Add(new AuditLog { Action = "workspace.item.saved", Resource = $"tender:{tenderId}", ActorKey = tenant.UserId.ToString(), Detail = $"organization={tenant.OrganizationId};stage={request.Stage}", IpAddress = http.Connection.RemoteIpAddress?.ToString() });
            await repository.SaveChangesAsync(ct);
            return Results.Ok(item);
        });

        group.MapDelete("/items/{tenderId:guid}", async (Guid tenderId, ClaimsPrincipal principal, IWorkspaceRepository repository, TenderScopeDbContext db, HttpContext http, CancellationToken ct) =>
        {
            var tenant = TenantContext.From(principal);
            if (tenant is null) return Results.Unauthorized();
            await repository.RemoveItemAsync(tenant.OrganizationId, tenderId, ct);
            db.AuditLogs.Add(new AuditLog { Action = "workspace.item.removed", Resource = $"tender:{tenderId}", ActorKey = tenant.UserId.ToString(), Detail = $"organization={tenant.OrganizationId}", IpAddress = http.Connection.RemoteIpAddress?.ToString() });
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
            db.AuditLogs.Add(new AuditLog { Action = "workspace.search.created", Resource = $"search:{saved.Id}", ActorKey = tenant.UserId.ToString(), Detail = $"organization={tenant.OrganizationId}", IpAddress = http.Connection.RemoteIpAddress?.ToString() });
            await repository.SaveChangesAsync(ct);
            return Results.Created($"/api/workspace/v2/searches/{saved.Id}", saved);
        });

        group.MapDelete("/searches/{id:guid}", async (Guid id, ClaimsPrincipal principal, IWorkspaceRepository repository, TenderScopeDbContext db, HttpContext http, CancellationToken ct) =>
        {
            var tenant = TenantContext.From(principal);
            if (tenant is null) return Results.Unauthorized();
            await repository.RemoveSearchAsync(tenant.OrganizationId, id, ct);
            db.AuditLogs.Add(new AuditLog { Action = "workspace.search.removed", Resource = $"search:{id}", ActorKey = tenant.UserId.ToString(), Detail = $"organization={tenant.OrganizationId}", IpAddress = http.Connection.RemoteIpAddress?.ToString() });
            await repository.SaveChangesAsync(ct);
            return Results.NoContent();
        });

        return endpoints;
    }

    private sealed record TenantContext(Guid UserId, Guid OrganizationId)
    {
        public static TenantContext? From(ClaimsPrincipal principal) =>
            Guid.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out var userId) &&
            Guid.TryParse(principal.FindFirstValue("organization_id"), out var organizationId)
                ? new TenantContext(userId, organizationId)
                : null;
    }
}

public sealed record WorkspaceItemRequestV2(OpportunityStage Stage, string? Notes);
public sealed record SavedSearchRequestV2(string Name, string? Query, string? Country, string? Category, bool? NotificationsEnabled);

using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using TenderScope.Domain.Entities;
using TenderScope.Infrastructure.Persistence;

namespace TenderScope.Api.Auth;

public static class OrganizationModule
{
    public static IEndpointRouteBuilder MapOrganizationManagement(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/organization").RequireAuthorization();

        group.MapGet("/members", async (ClaimsPrincipal principal, TenderScopeDbContext db, CancellationToken ct) =>
        {
            var context = await ResolveContextAsync(principal, db, ct);
            if (context is null) return Results.Unauthorized();
            var members = await (from membership in db.OrganizationMemberships.AsNoTracking()
                                 join user in db.Users.AsNoTracking() on membership.UserId equals user.Id
                                 where membership.OrganizationId == context.OrganizationId
                                 orderby membership.Role descending, user.DisplayName
                                 select new { membership.Id, user.Id, user.Email, user.DisplayName, membership.Role, membership.JoinedAt, user.LastLoginAt, user.IsActive })
                .ToListAsync(ct);
            return Results.Ok(members);
        });

        group.MapGet("/invitations", async (ClaimsPrincipal principal, TenderScopeDbContext db, CancellationToken ct) =>
        {
            var context = await RequireManagerAsync(principal, db, ct);
            if (context is null) return Results.Forbid();
            var invitations = await db.OrganizationInvitations.AsNoTracking()
                .Where(x => x.OrganizationId == context.OrganizationId && x.AcceptedAt == null && x.RevokedAt == null)
                .OrderByDescending(x => x.CreatedAt)
                .Select(x => new { x.Id, x.Email, x.Role, x.CreatedAt, x.ExpiresAt, x.InvitedByUserId, x.IsActive })
                .ToListAsync(ct);
            return Results.Ok(invitations);
        });

        group.MapPost("/invitations", async (InviteMemberRequest request, ClaimsPrincipal principal, TenderScopeDbContext db, HttpContext http, CancellationToken ct) =>
        {
            var context = await RequireManagerAsync(principal, db, ct);
            if (context is null) return Results.Forbid();
            var email = request.Email.Trim().ToLowerInvariant();
            if (!Regex.IsMatch(email, "^[^@\\s]+@[^@\\s]+\\.[^@\\s]+$", RegexOptions.CultureInvariant))
                return Results.ValidationProblem(new Dictionary<string, string[]> { ["email"] = ["Provide a valid email address."] });
            if (request.Role is OrganizationRole.Owner || request.Role > context.Role)
                return Results.BadRequest(new { error = "You cannot invite a member with this role." });

            var existingUserId = await db.Users.Where(x => x.Email == email).Select(x => (Guid?)x.Id).SingleOrDefaultAsync(ct);
            if (existingUserId.HasValue && await db.OrganizationMemberships.AnyAsync(x => x.OrganizationId == context.OrganizationId && x.UserId == existingUserId.Value, ct))
                return Results.Conflict(new { error = "This user is already a member." });

            var now = DateTimeOffset.UtcNow;
            var stale = await db.OrganizationInvitations.Where(x => x.OrganizationId == context.OrganizationId && x.Email == email && x.AcceptedAt == null && x.RevokedAt == null).ToListAsync(ct);
            foreach (var item in stale) item.Revoke(now);

            var rawToken = Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLowerInvariant();
            var invitation = new OrganizationInvitation
            {
                OrganizationId = context.OrganizationId,
                Email = email,
                Role = request.Role,
                TokenHash = Hash(rawToken),
                InvitedByUserId = context.UserId,
                ExpiresAt = now.AddDays(Math.Clamp(request.ExpiresInDays ?? 7, 1, 30))
            };
            db.OrganizationInvitations.Add(invitation);
            AddAudit(db, "organization.invitation.created", $"invitation:{invitation.Id}", context.UserId, http, $"email={email};role={request.Role}");
            await db.SaveChangesAsync(ct);
            return Results.Created($"/api/organization/invitations/{invitation.Id}", new
            {
                invitation.Id, invitation.Email, invitation.Role, invitation.ExpiresAt,
                token = rawToken,
                acceptPath = $"/api/organization/invitations/accept"
            });
        });

        group.MapPost("/invitations/accept", async (AcceptInvitationRequest request, ClaimsPrincipal principal, TenderScopeDbContext db, HttpContext http, CancellationToken ct) =>
        {
            var context = await ResolveContextAsync(principal, db, ct, requireCurrentOrganization: false);
            if (context is null) return Results.Unauthorized();
            var tokenHash = Hash(request.Token.Trim());
            var invitation = await db.OrganizationInvitations.SingleOrDefaultAsync(x => x.TokenHash == tokenHash, ct);
            if (invitation is null || !invitation.IsActive) return Results.BadRequest(new { error = "Invitation is invalid or expired." });
            var user = await db.Users.SingleAsync(x => x.Id == context.UserId, ct);
            if (!string.Equals(user.Email, invitation.Email, StringComparison.OrdinalIgnoreCase)) return Results.Forbid();
            if (!await db.OrganizationMemberships.AnyAsync(x => x.OrganizationId == invitation.OrganizationId && x.UserId == user.Id, ct))
                db.OrganizationMemberships.Add(new OrganizationMembership { OrganizationId = invitation.OrganizationId, UserId = user.Id }.WithRole(invitation.Role));
            invitation.Accept(DateTimeOffset.UtcNow);
            AddAudit(db, "organization.invitation.accepted", $"invitation:{invitation.Id}", user.Id, http, null);
            await db.SaveChangesAsync(ct);
            return Results.Ok(new { organizationId = invitation.OrganizationId, role = invitation.Role.ToString() });
        });

        group.MapPatch("/members/{userId:guid}/role", async (Guid userId, ChangeRoleRequest request, ClaimsPrincipal principal, TenderScopeDbContext db, HttpContext http, CancellationToken ct) =>
        {
            var context = await RequireManagerAsync(principal, db, ct);
            if (context is null) return Results.Forbid();
            var target = await db.OrganizationMemberships.SingleOrDefaultAsync(x => x.OrganizationId == context.OrganizationId && x.UserId == userId, ct);
            if (target is null) return Results.NotFound();
            if (target.Role == OrganizationRole.Owner || request.Role == OrganizationRole.Owner || request.Role > context.Role || target.Role >= context.Role)
                return Results.BadRequest(new { error = "This role change is not permitted." });
            target.ChangeRole(request.Role);
            AddAudit(db, "organization.member.role_changed", $"user:{userId}", context.UserId, http, $"role={request.Role}");
            await db.SaveChangesAsync(ct);
            return Results.Ok(new { userId, role = target.Role.ToString() });
        });

        group.MapDelete("/members/{userId:guid}", async (Guid userId, ClaimsPrincipal principal, TenderScopeDbContext db, HttpContext http, CancellationToken ct) =>
        {
            var context = await RequireManagerAsync(principal, db, ct);
            if (context is null) return Results.Forbid();
            var target = await db.OrganizationMemberships.SingleOrDefaultAsync(x => x.OrganizationId == context.OrganizationId && x.UserId == userId, ct);
            if (target is null) return Results.NotFound();
            if (target.Role == OrganizationRole.Owner || target.Role >= context.Role || userId == context.UserId)
                return Results.BadRequest(new { error = "This member cannot be removed." });
            db.OrganizationMemberships.Remove(target);
            AddAudit(db, "organization.member.removed", $"user:{userId}", context.UserId, http, null);
            await db.SaveChangesAsync(ct);
            return Results.NoContent();
        });

        group.MapDelete("/invitations/{id:guid}", async (Guid id, ClaimsPrincipal principal, TenderScopeDbContext db, HttpContext http, CancellationToken ct) =>
        {
            var context = await RequireManagerAsync(principal, db, ct);
            if (context is null) return Results.Forbid();
            var invitation = await db.OrganizationInvitations.SingleOrDefaultAsync(x => x.Id == id && x.OrganizationId == context.OrganizationId, ct);
            if (invitation is null) return Results.NotFound();
            if (invitation.IsActive) invitation.Revoke(DateTimeOffset.UtcNow);
            AddAudit(db, "organization.invitation.revoked", $"invitation:{id}", context.UserId, http, null);
            await db.SaveChangesAsync(ct);
            return Results.NoContent();
        });

        return endpoints;
    }

    private static async Task<OrganizationContext?> RequireManagerAsync(ClaimsPrincipal principal, TenderScopeDbContext db, CancellationToken ct)
    {
        var context = await ResolveContextAsync(principal, db, ct);
        return context is not null && context.Role >= OrganizationRole.Manager ? context : null;
    }

    private static async Task<OrganizationContext?> ResolveContextAsync(ClaimsPrincipal principal, TenderScopeDbContext db, CancellationToken ct, bool requireCurrentOrganization = true)
    {
        if (!Guid.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out var userId)) return null;
        Guid organizationId = Guid.Empty;
        if (requireCurrentOrganization && !Guid.TryParse(principal.FindFirstValue("organization_id"), out organizationId)) return null;
        var query = db.OrganizationMemberships.AsNoTracking().Where(x => x.UserId == userId);
        if (requireCurrentOrganization) query = query.Where(x => x.OrganizationId == organizationId);
        var membership = await query.OrderByDescending(x => x.Role).FirstOrDefaultAsync(ct);
        return membership is null ? null : new OrganizationContext(userId, membership.OrganizationId, membership.Role);
    }

    private static string Hash(string token) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token))).ToLowerInvariant();
    private static void AddAudit(TenderScopeDbContext db, string action, string resource, Guid actor, HttpContext http, string? detail) =>
        db.AuditLogs.Add(new AuditLog { Action = action, Resource = resource, ActorKey = actor.ToString(), IpAddress = http.Connection.RemoteIpAddress?.ToString(), Detail = detail });

    private sealed record OrganizationContext(Guid UserId, Guid OrganizationId, OrganizationRole Role);
}

internal static class MembershipFactoryExtensions
{
    public static OrganizationMembership WithRole(this OrganizationMembership membership, OrganizationRole role)
    {
        membership.ChangeRole(role);
        return membership;
    }
}

public sealed record InviteMemberRequest(string Email, OrganizationRole Role, int? ExpiresInDays);
public sealed record AcceptInvitationRequest(string Token);
public sealed record ChangeRoleRequest(OrganizationRole Role);

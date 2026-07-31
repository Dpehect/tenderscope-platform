using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using TenderScope.Domain.Entities;
using TenderScope.Infrastructure.Persistence;

namespace TenderScope.Api.Auth;

public static class SecurityModule
{
    public static IEndpointRouteBuilder MapSecurityManagement(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/security").RequireAuthorization();

        group.MapGet("/sessions", async (ClaimsPrincipal principal, TenderScopeDbContext db, CancellationToken ct) =>
        {
            if (!Guid.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out var userId)) return Results.Unauthorized();
            var sessions = await db.RefreshTokens.AsNoTracking()
                .Where(x => x.UserId == userId && x.RevokedAt == null && x.ExpiresAt > DateTimeOffset.UtcNow)
                .OrderByDescending(x => x.CreatedAt)
                .Select(x => new { x.Id, x.OrganizationId, x.CreatedAt, x.ExpiresAt })
                .ToListAsync(ct);
            return Results.Ok(sessions);
        });

        group.MapDelete("/sessions/{id:guid}", async (Guid id, ClaimsPrincipal principal, TenderScopeDbContext db, CancellationToken ct) =>
        {
            if (!Guid.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out var userId)) return Results.Unauthorized();
            var token = await db.RefreshTokens.SingleOrDefaultAsync(x => x.Id == id && x.UserId == userId, ct);
            if (token is null) return Results.NotFound();
            if (token.IsActive) token.Revoke(DateTimeOffset.UtcNow);
            await db.SaveChangesAsync(ct);
            return Results.NoContent();
        });

        group.MapPost("/sessions/revoke-all", async (ClaimsPrincipal principal, TenderScopeDbContext db, CancellationToken ct) =>
        {
            if (!Guid.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out var userId)) return Results.Unauthorized();
            var tokens = await db.RefreshTokens.Where(x => x.UserId == userId && x.RevokedAt == null && x.ExpiresAt > DateTimeOffset.UtcNow).ToListAsync(ct);
            foreach (var token in tokens) token.Revoke(DateTimeOffset.UtcNow);
            await db.SaveChangesAsync(ct);
            return Results.Ok(new { revoked = tokens.Count });
        });

        group.MapPost("/change-password", async (ChangePasswordRequest request, ClaimsPrincipal principal, TenderScopeDbContext db, PasswordService passwords, CancellationToken ct) =>
        {
            if (!Guid.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out var userId)) return Results.Unauthorized();
            if (request.NewPassword.Length < 10) return Results.ValidationProblem(new Dictionary<string, string[]> { ["newPassword"] = ["Password must contain at least 10 characters."] });
            var user = await db.Users.SingleOrDefaultAsync(x => x.Id == userId && x.IsActive, ct);
            if (user is null || !passwords.Verify(request.CurrentPassword, user.PasswordHash)) return Results.Json(new { error = "Current password is incorrect." }, statusCode: 400);
            user.ChangePassword(passwords.Hash(request.NewPassword));
            var tokens = await db.RefreshTokens.Where(x => x.UserId == userId && x.RevokedAt == null).ToListAsync(ct);
            foreach (var token in tokens) token.Revoke(DateTimeOffset.UtcNow);
            await db.SaveChangesAsync(ct);
            return Results.Ok(new { changed = true, sessionsRevoked = tokens.Count });
        }).RequireRateLimiting("auth");

        return endpoints;
    }
}

public sealed record ChangePasswordRequest(string CurrentPassword, string NewPassword);

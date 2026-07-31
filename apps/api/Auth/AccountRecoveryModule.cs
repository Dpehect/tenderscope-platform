using System.Data;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using TenderScope.Infrastructure.Persistence;

namespace TenderScope.Api.Auth;

public static class AccountRecoveryModule
{
    public static IEndpointRouteBuilder MapAccountRecovery(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/account");

        group.MapGet("/status", async (ClaimsPrincipal principal, TenderScopeDbContext db, CancellationToken ct) =>
        {
            if (!Guid.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out var userId)) return Results.Unauthorized();
            var connection = db.Database.GetDbConnection();
            await OpenAsync(connection, ct);
            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT \"EmailVerifiedAt\" FROM app_users WHERE \"Id\" = @userId";
            Add(command, "userId", userId);
            var value = await command.ExecuteScalarAsync(ct);
            return Results.Ok(new { emailVerified = value is not null and not DBNull, emailVerifiedAt = value is DBNull ? null : value });
        }).RequireAuthorization();

        group.MapPost("/email-verification/request", async (ClaimsPrincipal principal, TenderScopeDbContext db, IConfiguration configuration, HttpContext http, CancellationToken ct) =>
        {
            if (!Guid.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out var userId)) return Results.Unauthorized();
            var token = await IssueTokenAsync(db, userId, "email-verification", TimeSpan.FromHours(24), http.Connection.RemoteIpAddress?.ToString(), ct);
            return Results.Accepted(value: BuildDeliveryResponse(configuration, token, "/verify-email?token="));
        }).RequireAuthorization().RequireRateLimiting("auth");

        group.MapPost("/email-verification/confirm", async (ConfirmTokenRequest request, TenderScopeDbContext db, CancellationToken ct) =>
        {
            var userId = await ConsumeTokenAsync(db, request.Token, "email-verification", ct);
            if (userId is null) return Results.BadRequest(new { error = "Verification token is invalid or expired." });
            var connection = db.Database.GetDbConnection();
            await OpenAsync(connection, ct);
            await using var command = connection.CreateCommand();
            command.CommandText = "UPDATE app_users SET \"EmailVerifiedAt\" = COALESCE(\"EmailVerifiedAt\", now()) WHERE \"Id\" = @userId";
            Add(command, "userId", userId.Value);
            await command.ExecuteNonQueryAsync(ct);
            return Results.Ok(new { verified = true });
        }).RequireRateLimiting("auth");

        group.MapPost("/password-reset/request", async (PasswordResetRequest request, TenderScopeDbContext db, IConfiguration configuration, HttpContext http, CancellationToken ct) =>
        {
            var email = request.Email.Trim().ToLowerInvariant();
            var connection = db.Database.GetDbConnection();
            await OpenAsync(connection, ct);
            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT \"Id\" FROM app_users WHERE \"Email\" = @email AND \"IsActive\" = true";
            Add(command, "email", email);
            var value = await command.ExecuteScalarAsync(ct);
            object response = new { accepted = true };
            if (value is Guid userId)
            {
                var token = await IssueTokenAsync(db, userId, "password-reset", TimeSpan.FromMinutes(30), http.Connection.RemoteIpAddress?.ToString(), ct);
                response = BuildDeliveryResponse(configuration, token, "/reset-password?token=");
            }
            return Results.Accepted(value: response);
        }).RequireRateLimiting("auth");

        group.MapPost("/password-reset/confirm", async (PasswordResetConfirmRequest request, TenderScopeDbContext db, PasswordService passwords, CancellationToken ct) =>
        {
            if (request.NewPassword.Length < 10) return Results.ValidationProblem(new Dictionary<string, string[]> { ["newPassword"] = ["Password must contain at least 10 characters."] });
            var userId = await ConsumeTokenAsync(db, request.Token, "password-reset", ct);
            if (userId is null) return Results.BadRequest(new { error = "Reset token is invalid or expired." });
            var user = await db.Users.SingleOrDefaultAsync(x => x.Id == userId.Value && x.IsActive, ct);
            if (user is null) return Results.BadRequest(new { error = "Reset token is invalid or expired." });
            user.ChangePassword(passwords.Hash(request.NewPassword));
            var sessions = await db.RefreshTokens.Where(x => x.UserId == user.Id && x.RevokedAt == null).ToListAsync(ct);
            foreach (var session in sessions) session.Revoke(DateTimeOffset.UtcNow);
            await db.SaveChangesAsync(ct);
            return Results.Ok(new { reset = true, sessionsRevoked = sessions.Count });
        }).RequireRateLimiting("auth");

        return endpoints;
    }

    private static object BuildDeliveryResponse(IConfiguration configuration, string token, string path)
    {
        var expose = configuration.GetValue("AccountRecovery:ExposeTokens", false);
        var site = configuration["AccountRecovery:SiteUrl"]?.TrimEnd('/') ?? "http://localhost:3000";
        return expose ? new { accepted = true, actionUrl = $"{site}{path}{Uri.EscapeDataString(token)}" } : new { accepted = true };
    }

    private static async Task<string> IssueTokenAsync(TenderScopeDbContext db, Guid userId, string purpose, TimeSpan lifetime, string? ip, CancellationToken ct)
    {
        var raw = Convert.ToBase64String(RandomNumberGenerator.GetBytes(48));
        var hash = Hash(raw);
        var connection = db.Database.GetDbConnection();
        await OpenAsync(connection, ct);
        await using var command = connection.CreateCommand();
        command.CommandText = """
UPDATE account_action_tokens SET "ConsumedAt" = now()
WHERE "UserId" = @userId AND "Purpose" = @purpose AND "ConsumedAt" IS NULL;
INSERT INTO account_action_tokens ("Id", "UserId", "Purpose", "TokenHash", "CreatedAt", "ExpiresAt", "RequestedIp")
VALUES (@id, @userId, @purpose, @hash, now(), @expiresAt, @ip);
""";
        Add(command, "id", Guid.NewGuid()); Add(command, "userId", userId); Add(command, "purpose", purpose); Add(command, "hash", hash); Add(command, "expiresAt", DateTimeOffset.UtcNow.Add(lifetime)); Add(command, "ip", ip);
        await command.ExecuteNonQueryAsync(ct);
        return raw;
    }

    private static async Task<Guid?> ConsumeTokenAsync(TenderScopeDbContext db, string rawToken, string purpose, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(rawToken)) return null;
        var connection = db.Database.GetDbConnection();
        await OpenAsync(connection, ct);
        await using var command = connection.CreateCommand();
        command.CommandText = """
UPDATE account_action_tokens
SET "ConsumedAt" = now()
WHERE "Id" = (
  SELECT "Id" FROM account_action_tokens
  WHERE "TokenHash" = @hash AND "Purpose" = @purpose AND "ConsumedAt" IS NULL AND "ExpiresAt" > now()
  FOR UPDATE SKIP LOCKED LIMIT 1
)
RETURNING "UserId";
""";
        Add(command, "hash", Hash(rawToken)); Add(command, "purpose", purpose);
        var value = await command.ExecuteScalarAsync(ct);
        return value is Guid id ? id : null;
    }

    private static string Hash(string token) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token))).ToLowerInvariant();
    private static async Task OpenAsync(System.Data.Common.DbConnection connection, CancellationToken ct) { if (connection.State != ConnectionState.Open) await connection.OpenAsync(ct); }
    private static void Add(System.Data.Common.DbCommand command, string name, object? value) { var parameter = command.CreateParameter(); parameter.ParameterName = name; parameter.Value = value ?? DBNull.Value; command.Parameters.Add(parameter); }
}

public sealed record ConfirmTokenRequest(string Token);
public sealed record PasswordResetRequest(string Email);
public sealed record PasswordResetConfirmRequest(string Token, string NewPassword);

using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using TenderScope.Domain.Entities;
using TenderScope.Infrastructure.Persistence;

namespace TenderScope.Api.Auth;

public static class AuthModule
{
    private const string RefreshCookie = "tenderscope_refresh";

    public static IServiceCollection AddTenderScopeAuth(this IServiceCollection services, IConfiguration configuration)
    {
        var secret = configuration["Jwt:Secret"];
        if (string.IsNullOrWhiteSpace(secret) || secret.Length < 32)
            throw new InvalidOperationException("Jwt:Secret must contain at least 32 characters.");

        services.AddSingleton<PasswordService>();
        services.AddScoped<TokenService>();
        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(options =>
        {
            options.RequireHttpsMetadata = !configuration.GetValue("Jwt:AllowHttp", false);
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                ValidIssuer = configuration["Jwt:Issuer"] ?? "TenderScope",
                ValidAudience = configuration["Jwt:Audience"] ?? "TenderScope.Web",
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret)),
                ClockSkew = TimeSpan.FromSeconds(30)
            };
        });
        services.AddAuthorization();
        return services;
    }

    public static IEndpointRouteBuilder MapTenderScopeAuth(this IEndpointRouteBuilder endpoints)
    {
        var auth = endpoints.MapGroup("/api/auth");

        auth.MapPost("/register", async (RegisterRequest request, TenderScopeDbContext db, PasswordService passwords, TokenService tokens, HttpContext http, CancellationToken ct) =>
        {
            var email = request.Email.Trim().ToLowerInvariant();
            if (!IsValidEmail(email) || request.Password.Length < 10 || request.DisplayName.Trim().Length is < 2 or > 160)
                return Results.ValidationProblem(new Dictionary<string, string[]> { ["credentials"] = ["Provide a valid email, display name and a password of at least 10 characters."] });
            if (await db.Users.AnyAsync(x => x.Email == email, ct)) return Results.Conflict(new { error = "An account already exists for this email." });

            await using var transaction = await db.Database.BeginTransactionAsync(ct);
            var user = AppUser.Create(email, request.DisplayName, passwords.Hash(request.Password));
            var organizationName = string.IsNullOrWhiteSpace(request.OrganizationName) ? $"{request.DisplayName.Trim()}'s workspace" : request.OrganizationName.Trim();
            var organization = new Organization { Name = organizationName, Slug = await UniqueSlugAsync(db, organizationName, ct) };
            db.Users.Add(user);
            db.Organizations.Add(organization);
            db.OrganizationMemberships.Add(new OrganizationMembership { UserId = user.Id, OrganizationId = organization.Id });
            await db.SaveChangesAsync(ct);

            var membership = await db.OrganizationMemberships.SingleAsync(x => x.UserId == user.Id && x.OrganizationId == organization.Id, ct);
            membership.ChangeRole(OrganizationRole.Owner);
            user.MarkLogin(DateTimeOffset.UtcNow);
            var session = await tokens.CreateSessionAsync(user, membership, organization, http, ct);
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            SetRefreshCookie(http, session.RefreshToken, session.RefreshExpiresAt);
            return Results.Created("/api/auth/me", session.Response);
        }).RequireRateLimiting("auth");

        auth.MapPost("/login", async (LoginRequest request, TenderScopeDbContext db, PasswordService passwords, TokenService tokens, HttpContext http, CancellationToken ct) =>
        {
            var email = request.Email.Trim().ToLowerInvariant();
            var user = await db.Users.SingleOrDefaultAsync(x => x.Email == email, ct);
            if (user is null || !user.IsActive || !passwords.Verify(request.Password, user.PasswordHash))
                return Results.Json(new { error = "Invalid email or password." }, statusCode: StatusCodes.Status401Unauthorized);

            var membership = await db.OrganizationMemberships.Where(x => x.UserId == user.Id)
                .OrderByDescending(x => x.Role).FirstOrDefaultAsync(ct);
            if (membership is null) return Results.Forbid();
            var organization = await db.Organizations.SingleAsync(x => x.Id == membership.OrganizationId, ct);
            if (!organization.IsActive) return Results.Forbid();

            user.MarkLogin(DateTimeOffset.UtcNow);
            var session = await tokens.CreateSessionAsync(user, membership, organization, http, ct);
            await db.SaveChangesAsync(ct);
            SetRefreshCookie(http, session.RefreshToken, session.RefreshExpiresAt);
            return Results.Ok(session.Response);
        }).RequireRateLimiting("auth");

        auth.MapPost("/refresh", async (TenderScopeDbContext db, TokenService tokens, HttpContext http, CancellationToken ct) =>
        {
            if (!http.Request.Cookies.TryGetValue(RefreshCookie, out var rawToken) || string.IsNullOrWhiteSpace(rawToken)) return Results.Unauthorized();
            var session = await tokens.RotateSessionAsync(rawToken, http, ct);
            if (session is null) { DeleteRefreshCookie(http); return Results.Unauthorized(); }
            await db.SaveChangesAsync(ct);
            SetRefreshCookie(http, session.RefreshToken, session.RefreshExpiresAt);
            return Results.Ok(session.Response);
        }).RequireRateLimiting("auth");

        auth.MapPost("/logout", async (TenderScopeDbContext db, TokenService tokens, HttpContext http, CancellationToken ct) =>
        {
            if (http.Request.Cookies.TryGetValue(RefreshCookie, out var rawToken)) await tokens.RevokeAsync(rawToken, ct);
            await db.SaveChangesAsync(ct);
            DeleteRefreshCookie(http);
            return Results.NoContent();
        });

        auth.MapGet("/me", async (ClaimsPrincipal principal, TenderScopeDbContext db, CancellationToken ct) =>
        {
            if (!Guid.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out var userId)) return Results.Unauthorized();
            var user = await db.Users.AsNoTracking().SingleOrDefaultAsync(x => x.Id == userId && x.IsActive, ct);
            if (user is null) return Results.Unauthorized();
            return Results.Ok(new
            {
                user.Id, user.Email, user.DisplayName,
                OrganizationId = principal.FindFirstValue("organization_id"),
                Organization = principal.FindFirstValue("organization_name"),
                Role = principal.FindFirstValue(ClaimTypes.Role)
            });
        }).RequireAuthorization();

        return endpoints;
    }

    private static bool IsValidEmail(string email) => Regex.IsMatch(email, "^[^@\\s]+@[^@\\s]+\\.[^@\\s]+$", RegexOptions.CultureInvariant);
    private static async Task<string> UniqueSlugAsync(TenderScopeDbContext db, string name, CancellationToken ct)
    {
        var baseSlug = Regex.Replace(name.Trim().ToLowerInvariant(), "[^a-z0-9]+", "-").Trim('-');
        if (baseSlug.Length < 3) baseSlug = "workspace";
        baseSlug = baseSlug[..Math.Min(baseSlug.Length, 100)];
        var slug = baseSlug; var suffix = 1;
        while (await db.Organizations.AnyAsync(x => x.Slug == slug, ct)) slug = $"{baseSlug}-{++suffix}";
        return slug;
    }
    private static void SetRefreshCookie(HttpContext http, string token, DateTimeOffset expires) => http.Response.Cookies.Append(RefreshCookie, token, new CookieOptions { HttpOnly = true, Secure = true, SameSite = SameSiteMode.Strict, Expires = expires, Path = "/api/auth" });
    private static void DeleteRefreshCookie(HttpContext http) => http.Response.Cookies.Delete(RefreshCookie, new CookieOptions { HttpOnly = true, Secure = true, SameSite = SameSiteMode.Strict, Path = "/api/auth" });
}

public sealed class PasswordService
{
    private const int Iterations = 210_000;
    public string Hash(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(16);
        var hash = Rfc2898DeriveBytes.Pbkdf2(password, salt, Iterations, HashAlgorithmName.SHA256, 32);
        return $"pbkdf2-sha256${Iterations}${Convert.ToBase64String(salt)}${Convert.ToBase64String(hash)}";
    }
    public bool Verify(string password, string encoded)
    {
        try
        {
            var parts = encoded.Split('$');
            if (parts.Length != 4 || parts[0] != "pbkdf2-sha256" || !int.TryParse(parts[1], out var iterations)) return false;
            var salt = Convert.FromBase64String(parts[2]);
            var expected = Convert.FromBase64String(parts[3]);
            var actual = Rfc2898DeriveBytes.Pbkdf2(password, salt, iterations, HashAlgorithmName.SHA256, expected.Length);
            return CryptographicOperations.FixedTimeEquals(actual, expected);
        }
        catch { return false; }
    }
}

public sealed class TokenService(TenderScopeDbContext db, IConfiguration configuration)
{
    public async Task<SessionResult> CreateSessionAsync(AppUser user, OrganizationMembership membership, Organization organization, HttpContext http, CancellationToken ct)
    {
        var rawRefresh = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));
        var refreshExpiry = DateTimeOffset.UtcNow.AddDays(Math.Clamp(configuration.GetValue("Jwt:RefreshDays", 14), 1, 60));
        db.RefreshTokens.Add(new RefreshToken { UserId = user.Id, TokenHash = HashToken(rawRefresh), ExpiresAt = refreshExpiry });
        await Task.CompletedTask;
        return Build(user, membership, organization, rawRefresh, refreshExpiry);
    }

    public async Task<SessionResult?> RotateSessionAsync(string rawRefresh, HttpContext http, CancellationToken ct)
    {
        var hash = HashToken(rawRefresh);
        var existing = await db.RefreshTokens.SingleOrDefaultAsync(x => x.TokenHash == hash, ct);
        if (existing is null || !existing.IsActive) return null;
        var user = await db.Users.SingleOrDefaultAsync(x => x.Id == existing.UserId && x.IsActive, ct);
        if (user is null) return null;
        var membership = await db.OrganizationMemberships.Where(x => x.UserId == user.Id).OrderByDescending(x => x.Role).FirstOrDefaultAsync(ct);
        if (membership is null) return null;
        var organization = await db.Organizations.SingleOrDefaultAsync(x => x.Id == membership.OrganizationId && x.IsActive, ct);
        if (organization is null) return null;

        var nextRaw = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));
        var nextHash = HashToken(nextRaw);
        var expiry = DateTimeOffset.UtcNow.AddDays(Math.Clamp(configuration.GetValue("Jwt:RefreshDays", 14), 1, 60));
        existing.Revoke(DateTimeOffset.UtcNow, nextHash);
        db.RefreshTokens.Add(new RefreshToken { UserId = user.Id, TokenHash = nextHash, ExpiresAt = expiry });
        return Build(user, membership, organization, nextRaw, expiry);
    }

    public async Task RevokeAsync(string rawRefresh, CancellationToken ct)
    {
        var hash = HashToken(rawRefresh);
        var token = await db.RefreshTokens.SingleOrDefaultAsync(x => x.TokenHash == hash, ct);
        if (token?.IsActive == true) token.Revoke(DateTimeOffset.UtcNow);
    }

    private SessionResult Build(AppUser user, OrganizationMembership membership, Organization organization, string refresh, DateTimeOffset refreshExpiry)
    {
        var now = DateTimeOffset.UtcNow;
        var accessExpiry = now.AddMinutes(Math.Clamp(configuration.GetValue("Jwt:AccessMinutes", 15), 5, 60));
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(configuration["Jwt:Secret"]!));
        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()), new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(JwtRegisteredClaimNames.Email, user.Email), new Claim(ClaimTypes.Name, user.DisplayName),
            new Claim(ClaimTypes.Role, membership.Role.ToString()), new Claim("organization_id", organization.Id.ToString()),
            new Claim("organization_name", organization.Name), new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };
        var jwt = new JwtSecurityToken(configuration["Jwt:Issuer"] ?? "TenderScope", configuration["Jwt:Audience"] ?? "TenderScope.Web", claims, now.UtcDateTime, accessExpiry.UtcDateTime, new SigningCredentials(key, SecurityAlgorithms.HmacSha256));
        var access = new JwtSecurityTokenHandler().WriteToken(jwt);
        return new SessionResult(new AuthResponse(access, accessExpiry, new AuthUser(user.Id, user.Email, user.DisplayName, organization.Id, organization.Name, membership.Role.ToString())), refresh, refreshExpiry);
    }
    private static string HashToken(string token) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token))).ToLowerInvariant();
}

public sealed record RegisterRequest(string Email, string Password, string DisplayName, string? OrganizationName);
public sealed record LoginRequest(string Email, string Password);
public sealed record AuthUser(Guid Id, string Email, string DisplayName, Guid OrganizationId, string OrganizationName, string Role);
public sealed record AuthResponse(string AccessToken, DateTimeOffset ExpiresAt, AuthUser User);
public sealed record SessionResult(AuthResponse Response, string RefreshToken, DateTimeOffset RefreshExpiresAt);

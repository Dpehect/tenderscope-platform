using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using TenderScope.Domain.Entities;
using TenderScope.Infrastructure.Persistence;

namespace TenderScope.Api.Auth;

public static class NotificationModule
{
    public static IEndpointRouteBuilder MapNotifications(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/notifications").RequireAuthorization();

        group.MapGet("/", async (bool? unreadOnly, int? take, ClaimsPrincipal principal, TenderScopeDbContext db, CancellationToken ct) =>
        {
            var tenant = TenantContext.From(principal);
            if (tenant is null) return Results.Unauthorized();
            var query = db.Notifications.AsNoTracking().Where(x => x.OrganizationId == tenant.OrganizationId && x.UserId == tenant.UserId);
            if (unreadOnly == true) query = query.Where(x => x.ReadAt == null);
            var items = await query.OrderByDescending(x => x.CreatedAt).Take(Math.Clamp(take ?? 50, 1, 200)).ToListAsync(ct);
            var unread = await db.Notifications.CountAsync(x => x.OrganizationId == tenant.OrganizationId && x.UserId == tenant.UserId && x.ReadAt == null, ct);
            return Results.Ok(new { unread, items });
        });

        group.MapPatch("/{id:guid}/read", async (Guid id, ClaimsPrincipal principal, TenderScopeDbContext db, CancellationToken ct) =>
        {
            var tenant = TenantContext.From(principal);
            if (tenant is null) return Results.Unauthorized();
            var item = await db.Notifications.SingleOrDefaultAsync(x => x.Id == id && x.OrganizationId == tenant.OrganizationId && x.UserId == tenant.UserId, ct);
            if (item is null) return Results.NotFound();
            item.MarkRead(DateTimeOffset.UtcNow);
            await db.SaveChangesAsync(ct);
            return Results.Ok(item);
        });

        group.MapPost("/read-all", async (ClaimsPrincipal principal, TenderScopeDbContext db, CancellationToken ct) =>
        {
            var tenant = TenantContext.From(principal);
            if (tenant is null) return Results.Unauthorized();
            var now = DateTimeOffset.UtcNow;
            var items = await db.Notifications.Where(x => x.OrganizationId == tenant.OrganizationId && x.UserId == tenant.UserId && x.ReadAt == null).ToListAsync(ct);
            foreach (var item in items) item.MarkRead(now);
            await db.SaveChangesAsync(ct);
            return Results.Ok(new { updated = items.Count });
        });

        group.MapGet("/preferences", async (ClaimsPrincipal principal, TenderScopeDbContext db, CancellationToken ct) =>
        {
            var tenant = TenantContext.From(principal);
            if (tenant is null) return Results.Unauthorized();
            var preference = await db.NotificationPreferences.AsNoTracking().SingleOrDefaultAsync(x => x.OrganizationId == tenant.OrganizationId && x.UserId == tenant.UserId, ct);
            return Results.Ok(new
            {
                inAppEnabled = preference?.InAppEnabled ?? true,
                watchlistMatchesEnabled = preference?.WatchlistMatchesEnabled ?? true,
                deadlineRemindersEnabled = preference?.DeadlineRemindersEnabled ?? true
            });
        });

        group.MapPut("/preferences", async (NotificationPreferenceRequest request, ClaimsPrincipal principal, TenderScopeDbContext db, CancellationToken ct) =>
        {
            var tenant = TenantContext.From(principal);
            if (tenant is null) return Results.Unauthorized();
            var preference = await db.NotificationPreferences.SingleOrDefaultAsync(x => x.OrganizationId == tenant.OrganizationId && x.UserId == tenant.UserId, ct);
            if (preference is null)
            {
                preference = new NotificationPreference { OrganizationId = tenant.OrganizationId, UserId = tenant.UserId };
                db.NotificationPreferences.Add(preference);
            }
            preference.Update(request.InAppEnabled, request.WatchlistMatchesEnabled, request.DeadlineRemindersEnabled);
            await db.SaveChangesAsync(ct);
            return Results.Ok(new
            {
                inAppEnabled = preference.InAppEnabled,
                watchlistMatchesEnabled = preference.WatchlistMatchesEnabled,
                deadlineRemindersEnabled = preference.DeadlineRemindersEnabled
            });
        });

        group.MapPost("/run-watchlist-matches", async (ClaimsPrincipal principal, TenderScopeDbContext db, CancellationToken ct) =>
        {
            var tenant = TenantContext.From(principal);
            if (tenant is null) return Results.Unauthorized();
            if (!principal.IsInRole("Manager") && !principal.IsInRole("Admin") && !principal.IsInRole("Owner")) return Results.Forbid();

            var searches = await db.SavedSearches.AsNoTracking().Where(x => x.OrganizationId == tenant.OrganizationId && x.NotificationsEnabled).ToListAsync(ct);
            var users = await db.OrganizationMemberships.AsNoTracking().Where(x => x.OrganizationId == tenant.OrganizationId).Select(x => x.UserId).ToListAsync(ct);
            var created = 0;

            foreach (var search in searches)
            {
                var candidates = db.Tenders.AsNoTracking().Where(x => x.PublishedAt >= DateTimeOffset.UtcNow.AddDays(-30));
                if (!string.IsNullOrWhiteSpace(search.Country)) candidates = candidates.Where(x => x.CountryCode == search.Country);
                if (!string.IsNullOrWhiteSpace(search.Category)) candidates = candidates.Where(x => x.Category != null && x.Category.ToLower() == search.Category.ToLower());
                if (!string.IsNullOrWhiteSpace(search.Query))
                {
                    var term = search.Query.ToLower();
                    candidates = candidates.Where(x => x.Title.ToLower().Contains(term) || x.BuyerName.ToLower().Contains(term) || (x.Description != null && x.Description.ToLower().Contains(term)));
                }

                var tenders = await candidates.OrderByDescending(x => x.PublishedAt).Take(100).ToListAsync(ct);
                foreach (var tender in tenders)
                {
                    if (await db.WatchlistMatches.AnyAsync(x => x.SavedSearchId == search.Id && x.TenderId == tender.Id, ct)) continue;
                    var score = Score(search, tender);
                    if (score < 40) continue;
                    db.WatchlistMatches.Add(new WatchlistMatch { OrganizationId = tenant.OrganizationId, SavedSearchId = search.Id, TenderId = tender.Id, Score = score, Reason = BuildReason(search, tender) });
                    foreach (var userId in users)
                    {
                        var preference = await db.NotificationPreferences.AsNoTracking().SingleOrDefaultAsync(x => x.OrganizationId == tenant.OrganizationId && x.UserId == userId, ct);
                        if (preference is { InAppEnabled: false } || preference is { WatchlistMatchesEnabled: false }) continue;
                        db.Notifications.Add(new AppNotification { OrganizationId = tenant.OrganizationId, UserId = userId, Type = "watchlist.match", Title = $"New match: {search.Name}", Message = tender.Title, ResourceUrl = $"/opportunities/{tender.Id}" });
                    }
                    created++;
                }
            }

            await db.SaveChangesAsync(ct);
            return Results.Ok(new { created, searches = searches.Count });
        });

        return endpoints;
    }

    private static int Score(SavedSearch search, Tender tender)
    {
        var score = 20;
        if (!string.IsNullOrWhiteSpace(search.Country) && string.Equals(search.Country, tender.CountryCode, StringComparison.OrdinalIgnoreCase)) score += 20;
        if (!string.IsNullOrWhiteSpace(search.Category) && string.Equals(search.Category, tender.Category, StringComparison.OrdinalIgnoreCase)) score += 20;
        if (!string.IsNullOrWhiteSpace(search.Query))
        {
            var term = search.Query.Trim();
            if (tender.Title.Contains(term, StringComparison.OrdinalIgnoreCase)) score += 35;
            else if (tender.BuyerName.Contains(term, StringComparison.OrdinalIgnoreCase) || tender.Description?.Contains(term, StringComparison.OrdinalIgnoreCase) == true) score += 20;
        }
        if (tender.DeadlineAt.HasValue && tender.DeadlineAt.Value > DateTimeOffset.UtcNow.AddDays(7)) score += 5;
        return Math.Min(score, 100);
    }

    private static string BuildReason(SavedSearch search, Tender tender) =>
        $"query={search.Query ?? "*"};country={tender.CountryCode};category={tender.Category ?? "none"}";

    private sealed record TenantContext(Guid UserId, Guid OrganizationId)
    {
        public static TenantContext? From(ClaimsPrincipal principal) =>
            Guid.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out var userId) && Guid.TryParse(principal.FindFirstValue("organization_id"), out var organizationId)
                ? new TenantContext(userId, organizationId) : null;
    }
}

public sealed record NotificationPreferenceRequest(bool InAppEnabled, bool WatchlistMatchesEnabled, bool DeadlineRemindersEnabled);

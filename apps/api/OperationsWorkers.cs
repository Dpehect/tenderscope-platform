using Microsoft.EntityFrameworkCore;
using TenderScope.Domain.Entities;
using TenderScope.Infrastructure.Persistence;

namespace TenderScope.Api;

public sealed class WatchlistMatchingWorker(IServiceScopeFactory scopeFactory, ILogger<WatchlistMatchingWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Delay(TimeSpan.FromMinutes(2), stoppingToken);
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var db = scope.ServiceProvider.GetRequiredService<TenderScopeDbContext>();
                var organizations = await db.SavedSearches.AsNoTracking().Where(x => x.NotificationsEnabled).Select(x => x.OrganizationId).Distinct().ToListAsync(stoppingToken);
                foreach (var organizationId in organizations)
                {
                    var searches = await db.SavedSearches.AsNoTracking().Where(x => x.OrganizationId == organizationId && x.NotificationsEnabled).ToListAsync(stoppingToken);
                    var users = await db.OrganizationMemberships.AsNoTracking().Where(x => x.OrganizationId == organizationId).Select(x => x.UserId).ToListAsync(stoppingToken);
                    foreach (var search in searches)
                    {
                        var candidates = db.Tenders.AsNoTracking().Where(x => x.PublishedAt >= DateTimeOffset.UtcNow.AddDays(-30));
                        if (!string.IsNullOrWhiteSpace(search.Country)) candidates = candidates.Where(x => x.CountryCode == search.Country);
                        if (!string.IsNullOrWhiteSpace(search.Category)) candidates = candidates.Where(x => x.Category != null && x.Category.ToLower() == search.Category.ToLower());
                        if (!string.IsNullOrWhiteSpace(search.Query)) { var term = search.Query.ToLower(); candidates = candidates.Where(x => x.Title.ToLower().Contains(term) || x.BuyerName.ToLower().Contains(term)); }
                        foreach (var tender in await candidates.OrderByDescending(x => x.PublishedAt).Take(100).ToListAsync(stoppingToken))
                        {
                            if (await db.WatchlistMatches.AnyAsync(x => x.SavedSearchId == search.Id && x.TenderId == tender.Id, stoppingToken)) continue;
                            db.WatchlistMatches.Add(new WatchlistMatch { OrganizationId = organizationId, SavedSearchId = search.Id, TenderId = tender.Id, Score = 70, Reason = "scheduled-match" });
                            foreach (var userId in users) db.Notifications.Add(new AppNotification { OrganizationId = organizationId, UserId = userId, Type = "watchlist.match", Title = $"New match: {search.Name}", Message = tender.Title, ResourceUrl = $"/opportunities/{tender.Id}" });
                        }
                    }
                }
                await db.SaveChangesAsync(stoppingToken);
                logger.LogInformation("Scheduled watchlist matching completed for {Organizations} organizations", organizations.Count);
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested) { logger.LogError(ex, "Scheduled watchlist matching failed"); }
            await Task.Delay(TimeSpan.FromHours(1), stoppingToken);
        }
    }
}

public sealed class MaintenanceWorker(IServiceScopeFactory scopeFactory, ILogger<MaintenanceWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var db = scope.ServiceProvider.GetRequiredService<TenderScopeDbContext>();
                var now = DateTimeOffset.UtcNow;
                var expiredTokens = await db.RefreshTokens.Where(x => x.ExpiresAt < now.AddDays(-7) || (x.RevokedAt != null && x.RevokedAt < now.AddDays(-30))).ToListAsync(stoppingToken);
                var oldNotifications = await db.Notifications.Where(x => x.ReadAt != null && x.ReadAt < now.AddDays(-90)).ToListAsync(stoppingToken);
                db.RefreshTokens.RemoveRange(expiredTokens);
                db.Notifications.RemoveRange(oldNotifications);
                await db.SaveChangesAsync(stoppingToken);
                logger.LogInformation("Maintenance removed {Tokens} tokens and {Notifications} notifications", expiredTokens.Count, oldNotifications.Count);
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested) { logger.LogError(ex, "Maintenance cycle failed"); }
            await Task.Delay(TimeSpan.FromHours(24), stoppingToken);
        }
    }
}

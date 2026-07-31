using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using TenderScope.Domain.Entities;
using TenderScope.Infrastructure.Persistence;

namespace TenderScope.Api.Auth;

public static class IntelligenceModule
{
    public static IEndpointRouteBuilder MapIntelligence(this IEndpointRouteBuilder endpoints)
    {
        var search = endpoints.MapGroup("/api/search").RequireAuthorization();
        search.MapGet("/global", async (string? q, string? country, string? category, int? take, ClaimsPrincipal principal, TenderScopeDbContext db, CancellationToken ct) =>
        {
            if (!TenantContext.TryRead(principal, out var tenant)) return Results.Unauthorized();
            var limit = Math.Clamp(take ?? 40, 1, 100);
            var query = db.Tenders.AsNoTracking().AsQueryable();
            if (!string.IsNullOrWhiteSpace(q))
            {
                var term = q.Trim().ToLower();
                query = query.Where(x => x.Title.ToLower().Contains(term) || x.BuyerName.ToLower().Contains(term) || (x.Description != null && x.Description.ToLower().Contains(term)));
            }
            if (!string.IsNullOrWhiteSpace(country)) query = query.Where(x => x.CountryCode == country.Trim().ToUpper());
            if (!string.IsNullOrWhiteSpace(category)) query = query.Where(x => x.Category != null && x.Category.ToLower() == category.Trim().ToLower());

            var tenders = await query.OrderByDescending(x => x.PublishedAt).Take(limit).Select(x => new
            {
                type = "tender", x.Id, x.Title, subtitle = x.BuyerName, x.CountryCode, x.Category, x.DeadlineAt, x.EstimatedValue, x.Currency,
                href = $"/opportunities/{x.Id}"
            }).ToListAsync(ct);

            var workspace = await (from item in db.WorkspaceItems.AsNoTracking()
                                   join tender in db.Tenders.AsNoTracking() on item.TenderId equals tender.Id
                                   where item.OrganizationId == tenant.OrganizationId && (string.IsNullOrWhiteSpace(q) || tender.Title.ToLower().Contains(q.Trim().ToLower()))
                                   orderby item.UpdatedAt descending
                                   select new { type = "workspace", id = item.Id, title = tender.Title, subtitle = item.Stage.ToString(), tender.CountryCode, tender.Category, tender.DeadlineAt, tender.EstimatedValue, tender.Currency, href = "/workspace" })
                .Take(20).ToListAsync(ct);

            var watchlists = await db.SavedSearches.AsNoTracking().Where(x => x.OrganizationId == tenant.OrganizationId && (string.IsNullOrWhiteSpace(q) || x.Name.ToLower().Contains(q.Trim().ToLower())))
                .OrderByDescending(x => x.CreatedAt).Take(20).Select(x => new { type = "watchlist", x.Id, title = x.Name, subtitle = x.Query ?? "All opportunities", countryCode = x.Country, x.Category, deadlineAt = (DateTimeOffset?)null, estimatedValue = (decimal?)null, currency = (string?)null, href = "/workspace" }).ToListAsync(ct);

            return Results.Ok(new { query = q ?? string.Empty, tenders, workspace, watchlists });
        });

        var intelligence = endpoints.MapGroup("/api/intelligence").RequireAuthorization();
        intelligence.MapGet("/tenders/{id:guid}", async (Guid id, TenderScopeDbContext db, CancellationToken ct) =>
        {
            var tender = await db.Tenders.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct);
            if (tender is null) return Results.NotFound();

            var similar = await db.Tenders.AsNoTracking()
                .Where(x => x.Id != id && (x.Category == tender.Category || x.BuyerName == tender.BuyerName || x.CountryCode == tender.CountryCode))
                .OrderByDescending(x => (x.Category == tender.Category ? 3 : 0) + (x.BuyerName == tender.BuyerName ? 2 : 0) + (x.CountryCode == tender.CountryCode ? 1 : 0))
                .ThenByDescending(x => x.PublishedAt).Take(8)
                .Select(x => new { x.Id, x.Title, x.BuyerName, x.CountryCode, x.Category, x.EstimatedValue, x.Currency, x.DeadlineAt })
                .ToListAsync(ct);

            var buyerHistory = await db.Tenders.AsNoTracking().Where(x => x.BuyerName == tender.BuyerName)
                .GroupBy(_ => 1).Select(g => new { notices = g.Count(), disclosedValue = g.Sum(x => x.EstimatedValue ?? 0), lastPublishedAt = g.Max(x => x.PublishedAt) }).SingleOrDefaultAsync(ct);
            var categoryValues = await db.Tenders.AsNoTracking().Where(x => x.Category == tender.Category && x.EstimatedValue != null).Select(x => x.EstimatedValue!.Value).ToListAsync(ct);
            var categoryAverage = categoryValues.Count == 0 ? 0 : categoryValues.Average();
            var deadlineDays = tender.DeadlineAt.HasValue ? (int)Math.Floor((tender.DeadlineAt.Value - DateTimeOffset.UtcNow).TotalDays) : (int?)null;
            var riskScore = 15;
            var risks = new List<string>();
            if (deadlineDays is <= 7) { riskScore += 35; risks.Add("Deadline is within seven days."); }
            if (!tender.EstimatedValue.HasValue) { riskScore += 15; risks.Add("Estimated value is not disclosed."); }
            if (string.IsNullOrWhiteSpace(tender.Description)) { riskScore += 10; risks.Add("Notice description is limited."); }
            if (buyerHistory?.notices < 3) { riskScore += 10; risks.Add("Buyer has limited historical data."); }
            if (tender.EstimatedValue.HasValue && categoryAverage > 0 && tender.EstimatedValue.Value > categoryAverage * 3) { riskScore += 10; risks.Add("Value is significantly above the category average."); }

            return Results.Ok(new
            {
                tender = new { tender.Id, tender.Title, tender.BuyerName, tender.CountryCode, tender.Category, tender.EstimatedValue, tender.Currency, tender.PublishedAt, tender.DeadlineAt },
                score = Math.Clamp(100 - riskScore, 0, 100), riskScore = Math.Clamp(riskScore, 0, 100), risks,
                buyer = buyerHistory ?? new { notices = 0, disclosedValue = 0m, lastPublishedAt = DateTimeOffset.MinValue },
                category = new { averageValue = categoryAverage, sampleSize = categoryValues.Count }, similar
            });
        });

        return endpoints;
    }

    private readonly record struct TenantContext(Guid UserId, Guid OrganizationId)
    {
        public static bool TryRead(ClaimsPrincipal principal, out TenantContext tenant)
        {
            if (Guid.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out var userId) && Guid.TryParse(principal.FindFirstValue("organization_id"), out var organizationId))
            { tenant = new TenantContext(userId, organizationId); return true; }
            tenant = default; return false;
        }
    }
}

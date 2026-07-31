using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using TenderScope.Domain.Entities;
using TenderScope.Infrastructure.Persistence;

namespace TenderScope.Api.Auth;

public static class AnalyticsModule
{
    public static IEndpointRouteBuilder MapOrganizationAnalytics(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/analytics/organization", async (ClaimsPrincipal principal, TenderScopeDbContext db, CancellationToken ct) =>
        {
            if (!Guid.TryParse(principal.FindFirstValue("organization_id"), out var organizationId)) return Results.Unauthorized();

            var items = await db.WorkspaceItems.AsNoTracking().Where(x => x.OrganizationId == organizationId).ToListAsync(ct);
            var members = await (from membership in db.OrganizationMemberships.AsNoTracking()
                                 join user in db.Users.AsNoTracking() on membership.UserId equals user.Id
                                 where membership.OrganizationId == organizationId
                                 select new { user.Id, user.DisplayName, membership.Role }).ToListAsync(ct);

            var total = items.Count;
            var won = items.Count(x => x.Stage == OpportunityStage.Won);
            var lost = items.Count(x => x.Stage == OpportunityStage.Lost);
            var decided = won + lost;
            var now = DateTimeOffset.UtcNow;
            var pipeline = Enum.GetValues<OpportunityStage>().Select(stage => new { stage = stage.ToString(), count = items.Count(x => x.Stage == stage) });
            var workload = members.Select(member => new
            {
                member.Id,
                member.DisplayName,
                role = member.Role.ToString(),
                assigned = items.Count(x => x.AssigneeUserId == member.Id),
                overdue = items.Count(x => x.AssigneeUserId == member.Id && x.InternalDeadline < now && x.Stage != OpportunityStage.Won && x.Stage != OpportunityStage.Lost)
            }).OrderByDescending(x => x.assigned);

            return Results.Ok(new
            {
                total,
                active = items.Count(x => x.Stage != OpportunityStage.Won && x.Stage != OpportunityStage.Lost),
                won,
                lost,
                winRate = decided == 0 ? 0 : Math.Round((decimal)won / decided * 100, 1),
                dueNext7Days = items.Count(x => x.InternalDeadline >= now && x.InternalDeadline <= now.AddDays(7)),
                overdue = items.Count(x => x.InternalDeadline < now && x.Stage != OpportunityStage.Won && x.Stage != OpportunityStage.Lost),
                pipeline,
                workload
            });
        }).RequireAuthorization();

        return endpoints;
    }
}

using Microsoft.EntityFrameworkCore;
using TenderScope.Domain.Entities;

namespace TenderScope.Infrastructure.Persistence;

public sealed class TenderScopeDbContext(DbContextOptions<TenderScopeDbContext> options) : DbContext(options)
{
    public DbSet<Tender> Tenders => Set<Tender>();
    public DbSet<TenderSource> Sources => Set<TenderSource>();
    public DbSet<WorkspaceItem> WorkspaceItems => Set<WorkspaceItem>();
    public DbSet<SavedSearch> SavedSearches => Set<SavedSearch>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<Organization> Organizations => Set<Organization>();
    public DbSet<AppUser> Users => Set<AppUser>();
    public DbSet<OrganizationMembership> OrganizationMemberships => Set<OrganizationMembership>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<OrganizationInvitation> OrganizationInvitations => Set<OrganizationInvitation>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var tender = modelBuilder.Entity<Tender>();
        tender.ToTable("tenders"); tender.HasKey(x => x.Id);
        tender.HasIndex(x => new { x.SourceKey, x.ExternalId }).IsUnique(); tender.HasIndex(x => x.PublishedAt); tender.HasIndex(x => new { x.CountryCode, x.Category, x.DeadlineAt });
        tender.Property(x => x.SourceUrl).HasConversion(x => x.ToString(), x => new Uri(x)); tender.Property(x => x.Title).HasMaxLength(500); tender.Property(x => x.CountryCode).HasMaxLength(3); tender.Property(x => x.Currency).HasMaxLength(3);

        var source = modelBuilder.Entity<TenderSource>();
        source.ToTable("tender_sources"); source.HasKey(x => x.Id); source.HasIndex(x => x.Key).IsUnique(); source.HasIndex(x => new { x.IsEnabled, x.NextCrawlAt });
        source.Property(x => x.Key).HasMaxLength(120); source.Property(x => x.Name).HasMaxLength(250); source.Property(x => x.CountryCode).HasMaxLength(3); source.Property(x => x.BaseUrl).HasConversion(x => x.ToString(), x => new Uri(x)); source.Property(x => x.LastError).HasMaxLength(2000);

        var workspace = modelBuilder.Entity<WorkspaceItem>();
        workspace.ToTable("workspace_items"); workspace.HasKey(x => x.Id); workspace.HasIndex(x => new { x.UserKey, x.TenderId }).IsUnique(); workspace.HasIndex(x => new { x.UserKey, x.Stage }); workspace.Property(x => x.UserKey).HasMaxLength(160); workspace.Property(x => x.Notes).HasMaxLength(4000);

        var search = modelBuilder.Entity<SavedSearch>();
        search.ToTable("saved_searches"); search.HasKey(x => x.Id); search.HasIndex(x => new { x.UserKey, x.CreatedAt }); search.Property(x => x.UserKey).HasMaxLength(160); search.Property(x => x.Name).HasMaxLength(180); search.Property(x => x.Query).HasMaxLength(500); search.Property(x => x.Country).HasMaxLength(3); search.Property(x => x.Category).HasMaxLength(120);

        var audit = modelBuilder.Entity<AuditLog>();
        audit.ToTable("audit_logs"); audit.HasKey(x => x.Id); audit.HasIndex(x => x.CreatedAt); audit.HasIndex(x => new { x.Resource, x.CreatedAt });
        audit.Property(x => x.Action).HasMaxLength(120); audit.Property(x => x.Resource).HasMaxLength(240); audit.Property(x => x.ActorKey).HasMaxLength(160); audit.Property(x => x.Detail).HasMaxLength(4000); audit.Property(x => x.IpAddress).HasMaxLength(64);

        var organization = modelBuilder.Entity<Organization>();
        organization.ToTable("organizations"); organization.HasKey(x => x.Id); organization.HasIndex(x => x.Slug).IsUnique();
        organization.Property(x => x.Name).HasMaxLength(180); organization.Property(x => x.Slug).HasMaxLength(120);

        var user = modelBuilder.Entity<AppUser>();
        user.ToTable("app_users"); user.HasKey(x => x.Id); user.HasIndex(x => x.Email).IsUnique();
        user.Property(x => x.Email).HasMaxLength(320); user.Property(x => x.DisplayName).HasMaxLength(160); user.Property(x => x.PasswordHash).HasMaxLength(1000);

        var membership = modelBuilder.Entity<OrganizationMembership>();
        membership.ToTable("organization_memberships"); membership.HasKey(x => x.Id); membership.HasIndex(x => new { x.OrganizationId, x.UserId }).IsUnique(); membership.HasIndex(x => new { x.UserId, x.Role });
        membership.HasOne<Organization>().WithMany().HasForeignKey(x => x.OrganizationId).OnDelete(DeleteBehavior.Cascade);
        membership.HasOne<AppUser>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);

        var refresh = modelBuilder.Entity<RefreshToken>();
        refresh.ToTable("refresh_tokens"); refresh.HasKey(x => x.Id); refresh.HasIndex(x => x.TokenHash).IsUnique(); refresh.HasIndex(x => new { x.UserId, x.ExpiresAt });
        refresh.Property(x => x.TokenHash).HasMaxLength(128); refresh.Property(x => x.ReplacedByTokenHash).HasMaxLength(128);
        refresh.HasOne<AppUser>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);

        var invitation = modelBuilder.Entity<OrganizationInvitation>();
        invitation.ToTable("organization_invitations"); invitation.HasKey(x => x.Id); invitation.HasIndex(x => x.TokenHash).IsUnique(); invitation.HasIndex(x => new { x.OrganizationId, x.Email, x.ExpiresAt });
        invitation.Property(x => x.Email).HasMaxLength(320); invitation.Property(x => x.TokenHash).HasMaxLength(128);
        invitation.HasOne<Organization>().WithMany().HasForeignKey(x => x.OrganizationId).OnDelete(DeleteBehavior.Cascade);
        invitation.HasOne<AppUser>().WithMany().HasForeignKey(x => x.InvitedByUserId).OnDelete(DeleteBehavior.Restrict);
    }
}

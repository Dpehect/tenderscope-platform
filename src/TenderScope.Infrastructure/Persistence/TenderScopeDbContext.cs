using Microsoft.EntityFrameworkCore;
using TenderScope.Domain.Entities;

namespace TenderScope.Infrastructure.Persistence;

public sealed class TenderScopeDbContext(DbContextOptions<TenderScopeDbContext> options) : DbContext(options)
{
    public DbSet<Tender> Tenders => Set<Tender>();
    public DbSet<TenderSource> Sources => Set<TenderSource>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var tender = modelBuilder.Entity<Tender>();
        tender.ToTable("tenders");
        tender.HasKey(x => x.Id);
        tender.HasIndex(x => new { x.SourceKey, x.ExternalId }).IsUnique();
        tender.HasIndex(x => x.PublishedAt);
        tender.HasIndex(x => new { x.CountryCode, x.Category, x.DeadlineAt });
        tender.Property(x => x.SourceUrl).HasConversion(x => x.ToString(), x => new Uri(x));
        tender.Property(x => x.Title).HasMaxLength(500);
        tender.Property(x => x.CountryCode).HasMaxLength(3);
        tender.Property(x => x.Currency).HasMaxLength(3);

        var source = modelBuilder.Entity<TenderSource>();
        source.ToTable("tender_sources");
        source.HasKey(x => x.Id);
        source.HasIndex(x => x.Key).IsUnique();
        source.HasIndex(x => new { x.IsEnabled, x.NextCrawlAt });
        source.Property(x => x.Key).HasMaxLength(120);
        source.Property(x => x.Name).HasMaxLength(250);
        source.Property(x => x.CountryCode).HasMaxLength(3);
        source.Property(x => x.BaseUrl).HasConversion(x => x.ToString(), x => new Uri(x));
        source.Property(x => x.LastError).HasMaxLength(2000);
    }
}

using Microsoft.EntityFrameworkCore;
using TenderScope.Domain.Entities;

namespace TenderScope.Infrastructure.Persistence;

public sealed class TenderScopeDbContext(DbContextOptions<TenderScopeDbContext> options) : DbContext(options)
{
    public DbSet<Tender> Tenders => Set<Tender>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var tender = modelBuilder.Entity<Tender>();
        tender.ToTable("tenders");
        tender.HasKey(x => x.Id);
        tender.HasIndex(x => new { x.SourceKey, x.ExternalId }).IsUnique();
        tender.HasIndex(x => x.PublishedAt);
        tender.Property(x => x.SourceUrl).HasConversion(x => x.ToString(), x => new Uri(x));
        tender.Property(x => x.Title).HasMaxLength(500);
        tender.Property(x => x.CountryCode).HasMaxLength(2);
        tender.Property(x => x.Currency).HasMaxLength(3);
    }
}

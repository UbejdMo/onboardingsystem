using MerchantOnboarding.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace MerchantOnboarding.Api.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options)
        : base(options)
    {
    }

    public DbSet<Merchant> Merchants => Set<Merchant>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Merchant>(entity =>
        {
            entity.HasKey(m => m.Id);

            // Stored as a string so rows stay readable in the database and
            // adding a new status later cannot renumber the existing ones.
            entity.Property(m => m.Status)
                .HasConversion<string>()
                .HasMaxLength(20)
                .IsRequired();

            entity.Property(m => m.Country)
                .HasColumnType("char(2)");

            // Compliance staff filter the queue by status constantly.
            entity.HasIndex(m => m.Status);
        });
    }
}

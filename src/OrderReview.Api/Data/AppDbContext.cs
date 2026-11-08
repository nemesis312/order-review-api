using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using OrderReview.Api.Models;

namespace OrderReview.Api.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Order> Orders => Set<Order>();

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        // SQLite stores DateTimeOffset as TEXT and EF cannot translate ORDER BY on it.
        // Persisting UTC ticks (INTEGER) keeps the instant exact and makes ordering work.
        configurationBuilder.Properties<DateTimeOffset>().HaveConversion<UtcTicksConverter>();
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Order>(entity =>
        {
            entity.ToTable("Orders");
            entity.HasKey(o => o.Id);

            entity.Property(o => o.CustomerId).IsRequired();
            entity.Property(o => o.Description).IsRequired().HasMaxLength(250);
            // SQLite has no decimal type (stored as TEXT); precision is documented and
            // enforced by providers that support it. Do not sort or filter by Total in SQL.
            entity.Property(o => o.Total).IsRequired().HasPrecision(18, 2);
            entity.Property(o => o.Status).IsRequired().HasConversion<string>().HasMaxLength(20);
            entity.Property(o => o.CreatedAt).IsRequired();
            entity.Property(o => o.CancellationReason).HasMaxLength(500);

            entity.HasIndex(o => o.CustomerId);
        });
    }

    private sealed class UtcTicksConverter() : ValueConverter<DateTimeOffset, long>(
        value => value.UtcTicks,
        ticks => new DateTimeOffset(ticks, TimeSpan.Zero));
}

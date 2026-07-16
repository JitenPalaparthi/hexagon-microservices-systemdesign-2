using Microsoft.EntityFrameworkCore;
using ProductRestServer.Entities;

namespace ProductRestServer.Data;

public sealed class ProductDbContext(DbContextOptions<ProductDbContext> options) : DbContext(options)
{
    public DbSet<ProductEntity> Products => Set<ProductEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var product = modelBuilder.Entity<ProductEntity>();
        product.ToTable("products");
        product.HasKey(x => x.Id);
        product.Property(x => x.Id).UseIdentityByDefaultColumn();
        product.Property(x => x.Name).HasMaxLength(200).IsRequired();
        product.Property(x => x.Description).HasMaxLength(1000);
        product.Property(x => x.Category).HasMaxLength(100).IsRequired();
        product.Property(x => x.Price).HasPrecision(18, 2);
        product.Property(x => x.AvailableQuantity).IsRequired();
        product.Property(x => x.CreatedAtUtc).HasColumnType("timestamp with time zone").IsRequired();
        product.HasIndex(x => x.Category);
    }
}

using Microsoft.EntityFrameworkCore;
using SalesChatbot.Data.Entities;

namespace SalesChatbot.Data;

public class SalesDbContext(DbContextOptions<SalesDbContext> options) : DbContext(options)
{
    public DbSet<Customer> Customers => Set<Customer>();

    public DbSet<Product> Products => Set<Product>();

    public DbSet<SalesOrder> Orders => Set<SalesOrder>();

    public DbSet<OrderItem> OrderItems => Set<OrderItem>();

    public DbSet<QueryAuditEntry> QueryAuditLog => Set<QueryAuditEntry>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Customer>(entity =>
        {
            entity.ToTable("Customers");
            entity.Property(e => e.Name).HasMaxLength(200).IsRequired();
            entity.Property(e => e.Country).HasMaxLength(100).IsRequired();
        });

        modelBuilder.Entity<Product>(entity =>
        {
            entity.ToTable("Products");
            entity.Property(e => e.Name).HasMaxLength(200).IsRequired();
            entity.Property(e => e.Category).HasMaxLength(100).IsRequired();
        });

        modelBuilder.Entity<SalesOrder>(entity =>
        {
            entity.ToTable("Orders");
            entity.Property(e => e.Status).HasMaxLength(50).IsRequired();
            entity.HasIndex(e => e.OrderDate);
            entity.HasIndex(e => e.Status);
            entity.HasIndex(e => e.CustomerId);
            entity.HasOne(e => e.Customer)
                .WithMany(c => c.Orders)
                .HasForeignKey(e => e.CustomerId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<OrderItem>(entity =>
        {
            entity.ToTable("OrderItems");
            entity.Property(e => e.UnitPrice).HasPrecision(18, 2);
            entity.HasOne(e => e.Order)
                .WithMany(o => o.OrderItems)
                .HasForeignKey(e => e.OrderId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.Product)
                .WithMany(p => p.OrderItems)
                .HasForeignKey(e => e.ProductId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<QueryAuditEntry>(entity =>
        {
            entity.ToTable("QueryAuditLog");
            entity.Property(e => e.UserQuestion).HasMaxLength(2000).IsRequired();
            entity.Property(e => e.GeneratedSql).HasMaxLength(4000).IsRequired();
            entity.Property(e => e.TimestampUtc).IsRequired();
            entity.HasIndex(e => e.TimestampUtc);
        });
    }
}

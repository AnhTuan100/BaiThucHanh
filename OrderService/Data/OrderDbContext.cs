using Microsoft.EntityFrameworkCore;
using OrderService.Models;

namespace OrderService.Data;

public sealed class OrderDbContext(DbContextOptions<OrderDbContext> options) : DbContext(options)
{
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<OrderItem> OrderItems => Set<OrderItem>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Order>(entity =>
        {
            entity.ToTable("orders");
            entity.Property(x => x.CustomerName).HasMaxLength(255).IsRequired();
            entity.Property(x => x.CustomerEmail).HasMaxLength(255).IsRequired();
            entity.Property(x => x.TotalAmount).HasPrecision(10, 2);
            entity.Property(x => x.Status).HasMaxLength(50).IsRequired();
            entity.HasMany(x => x.Items).WithOne(x => x.Order).HasForeignKey(x => x.OrderId)
                .OnDelete(DeleteBehavior.Cascade);
        });
        modelBuilder.Entity<OrderItem>(entity =>
        {
            entity.ToTable("order_items");
            entity.Property(x => x.ProductName).HasMaxLength(255).IsRequired();
            entity.Property(x => x.UnitPrice).HasPrecision(10, 2);
            entity.Property(x => x.TotalPrice).HasPrecision(10, 2);
        });
        foreach (var entity in modelBuilder.Model.GetEntityTypes())
            foreach (var property in entity.GetProperties())
                property.SetColumnName(System.Text.Json.JsonNamingPolicy.SnakeCaseLower.ConvertName(property.Name));
    }
}

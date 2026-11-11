using Microsoft.EntityFrameworkCore;
using OrderService.Models;

namespace OrderService.Data;

public class OrderDbContext(DbContextOptions<OrderDbContext> options) : DbContext(options)
{
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<OrderItem> OrderItems => Set<OrderItem>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Order>(entity =>
        {
            entity.ToTable("orders", table =>
            {
                table.HasCheckConstraint("CK_orders_status", "status IN ('pending', 'completed', 'cancelled')");
                table.HasCheckConstraint("CK_orders_total", "CAST(total_amount AS REAL) BETWEEN 0 AND 99999999.99");
            });
            entity.HasKey(o => o.Id);
            entity.Property(o => o.Id).HasColumnName("id");
            entity.Property(o => o.CustomerName).HasColumnName("customer_name").HasMaxLength(255).IsRequired();
            entity.Property(o => o.CustomerEmail).HasColumnName("customer_email").HasMaxLength(255).IsRequired();
            entity.Property(o => o.TotalAmount).HasColumnName("total_amount").HasColumnType("DECIMAL(10,2)");
            entity.Property(o => o.Status).HasColumnName("status").HasMaxLength(50).IsRequired();
            entity.Property(o => o.CreatedAt).HasColumnName("created_at");
            entity.Property(o => o.UpdatedAt).HasColumnName("updated_at");
            entity.HasMany(o => o.Items).WithOne().HasForeignKey(i => i.OrderId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<OrderItem>(entity =>
        {
            entity.ToTable("order_items", table =>
            {
                table.HasCheckConstraint("CK_order_items_quantity", "quantity > 0");
                table.HasCheckConstraint("CK_order_items_price", "CAST(unit_price AS REAL) >= 0 AND CAST(total_price AS REAL) BETWEEN 0 AND 99999999.99");
            });
            entity.HasKey(i => i.Id);
            entity.Property(i => i.Id).HasColumnName("id");
            entity.Property(i => i.OrderId).HasColumnName("order_id");
            entity.Property(i => i.ProductId).HasColumnName("product_id");
            entity.Property(i => i.ProductName).HasColumnName("product_name").HasMaxLength(255).IsRequired();
            entity.Property(i => i.Quantity).HasColumnName("quantity");
            entity.Property(i => i.UnitPrice).HasColumnName("unit_price").HasColumnType("DECIMAL(10,2)");
            entity.Property(i => i.TotalPrice).HasColumnName("total_price").HasColumnType("DECIMAL(10,2)");
        });
    }
}

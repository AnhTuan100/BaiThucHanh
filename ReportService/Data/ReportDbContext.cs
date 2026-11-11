using Microsoft.EntityFrameworkCore;
using ReportService.Models;

namespace ReportService.Data;

public sealed class ReportDbContext(DbContextOptions<ReportDbContext> options) : DbContext(options)
{
    public DbSet<OrderReport> OrderReports => Set<OrderReport>();
    public DbSet<ProductReport> ProductReports => Set<ProductReport>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<OrderReport>(entity =>
        {
            entity.ToTable("orders_reports");
            entity.Property(x => x.TotalRevenue).HasPrecision(10, 2);
            entity.Property(x => x.TotalCost).HasPrecision(10, 2);
            entity.Property(x => x.TotalProfit).HasPrecision(10, 2);
            entity.HasMany(x => x.ProductReports)
                  .WithOne(x => x.OrderReport)
                  .HasForeignKey(x => x.OrderReportId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ProductReport>(entity =>
        {
            entity.ToTable("product_reports");
            entity.Property(x => x.Revenue).HasPrecision(10, 2);
            entity.Property(x => x.Cost).HasPrecision(10, 2);
            entity.Property(x => x.Profit).HasPrecision(10, 2);
        });

        // Áp dụng snake_case cho tất cả cột
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
            foreach (var property in entityType.GetProperties())
                property.SetColumnName(
                    System.Text.Json.JsonNamingPolicy.SnakeCaseLower.ConvertName(property.Name));
    }
}

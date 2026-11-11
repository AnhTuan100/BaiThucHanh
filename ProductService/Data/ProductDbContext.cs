using Microsoft.EntityFrameworkCore;

namespace ProductService.Data;

public sealed class Product
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public decimal Price { get; set; }
    public int Quantity { get; set; }
}

public sealed class ProductDbContext(DbContextOptions<ProductDbContext> options) : DbContext(options)
{
    public DbSet<Product> Products => Set<Product>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Product>(entity =>
        {
            entity.ToTable("products");
            entity.Property(x => x.Name).HasMaxLength(255).IsRequired();
            entity.Property(x => x.Price).HasPrecision(10, 2);
            entity.HasData(
                new Product { Id = 1, Name = "Ban phim", Price = 250000m, Quantity = 20 },
                new Product { Id = 2, Name = "Chuot", Price = 150000m, Quantity = 30 },
                new Product { Id = 3, Name = "Man hinh", Price = 2500000m, Quantity = 0 });
        });
    }
}

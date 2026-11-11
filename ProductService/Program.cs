using Microsoft.EntityFrameworkCore;
using ProductService.Data;
using ServiceDefaults;

var builder = WebApplication.CreateBuilder(args);
builder.AddServiceApi("Product Service - ho tro bai thuc hanh 4");
builder.Services.AddDbContext<ProductDbContext>(options => options.UseSqlite(
    builder.Configuration.GetConnectionString("Products") ?? "Data Source=products.db"));
var app = builder.Build();
using (var scope = app.Services.CreateScope())
    scope.ServiceProvider.GetRequiredService<ProductDbContext>().Database.EnsureCreated();
app.UseServiceApi();
app.Run();

public partial class Program;

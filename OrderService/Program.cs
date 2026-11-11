using Microsoft.EntityFrameworkCore;
using OrderService.Data;
using OrderService.Services;
using ServiceDefaults;

var builder = WebApplication.CreateBuilder(args);
builder.AddServiceApi("Bai thuc hanh 4 - Order Management");
builder.Services.AddDbContext<OrderDbContext>(options => options.UseSqlite(
    builder.Configuration.GetConnectionString("Orders") ?? "Data Source=orders.db;Foreign Keys=True"));
builder.Services.AddHttpClient<ProductClient>(client =>
{
    client.BaseAddress = new Uri(builder.Configuration["Services:Products"] ?? "http://localhost:5001/");
    client.Timeout = TimeSpan.FromSeconds(5);
}).ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false });
builder.Services.AddScoped<OrderManager>();

var app = builder.Build();
using (var scope = app.Services.CreateScope())
    scope.ServiceProvider.GetRequiredService<OrderDbContext>().Database.EnsureCreated();
app.UseServiceApi();
app.Run();

public partial class Program;

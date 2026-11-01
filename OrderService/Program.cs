using Microsoft.EntityFrameworkCore;
using OrderService.Data;
using OrderService.Middlewares;
using OrderService.Services;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddControllers();
builder.Services.AddDbContext<OrderDbContext>(options => options.UseSqlite(
    builder.Configuration.GetConnectionString("OrderDb") ?? "Data Source=orders.db"));
builder.Services.AddHttpContextAccessor();
builder.Services.AddHttpClient("AuthService", client =>
{
    client.BaseAddress = new Uri(builder.Configuration["Services:AuthUrl"] ?? "http://localhost:5194/");
    client.Timeout = TimeSpan.FromSeconds(5);
});
builder.Services.AddHttpClient<ProductClient>(client =>
{
    client.BaseAddress = new Uri(builder.Configuration["Services:ProductUrl"] ?? "http://localhost:5001/");
    client.Timeout = TimeSpan.FromSeconds(5);
});
builder.Services.AddScoped<OrderManagementService>();

var app = builder.Build();
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<OrderDbContext>();
    db.Database.Migrate();
}

app.Use(async (context, next) =>
{
    try
    {
        await next(context);
    }
    catch (ApiException exception)
    {
        context.Response.StatusCode = exception.StatusCode;
        await context.Response.WriteAsJsonAsync(new { Success = false, Message = exception.Message });
    }
});
app.UseMiddleware<AuthServiceMiddleware>();
app.MapControllers();
app.Run();

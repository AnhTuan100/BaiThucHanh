using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;
using OrderService.Data;
using OrderService.Middlewares;
using OrderService.Services;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Order Management API", Version = "v1",
        Description = "Bài thực hành 4. Lấy JWT từ POST http://localhost:5194/login rồi nhập vào Authorize."
    });
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization", Type = SecuritySchemeType.Http, Scheme = "Bearer",
        BearerFormat = "JWT", In = ParameterLocation.Header
    });
    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        [new OpenApiSecurityScheme
        {
            Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
        }] = Array.Empty<string>()
    });
});
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

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
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
app.MapGet("/health", () => Results.Ok(new { Status = "Healthy", Service = "OrderService" }));
app.Run();

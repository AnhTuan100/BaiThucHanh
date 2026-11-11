using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;
using ProductService.Data;
using ProductService.Middlewares;
using ProductService.Services;

var builder = WebApplication.CreateBuilder(args);

// =============================================
// Đăng ký Controllers
// =============================================
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

// =============================================
// Cấu hình Swagger với hỗ trợ Bearer JWT
// =============================================
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title       = "Product Management API",
        Version     = "v1",
        Description = "Dịch vụ Quản lý Sản phẩm - Bài Thực Hành Số 3\n\n" +
                      "Xác thực: Lấy token từ Auth Service tại http://localhost:5194 (POST /login), " +
                      "sau đó nhập vào ô Authorize bên dưới."
    });

    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name        = "Authorization",
        Type        = SecuritySchemeType.Http,
        Scheme      = "Bearer",
        BearerFormat = "JWT",
        In          = ParameterLocation.Header,
        Description = "Nhập JWT token nhận được từ Auth Service (BaiThucHanh2 - cổng 5194)."
    });

    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
            },
            Array.Empty<string>()
        }
    });
});

// =============================================
// Cấu hình SQLite Database (riêng biệt cho dịch vụ này)
// =============================================
builder.Services.AddDbContext<ProductDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("ProductDb")
                      ?? "Data Source=products.db"));

// =============================================
// Đăng ký Service
// =============================================
builder.Services.AddScoped<IProductService, ProductManagementService>();

var app = builder.Build();

// =============================================
// Tự động tạo / migrate database khi khởi động
// =============================================
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ProductDbContext>();
    db.Database.Migrate();
}

// =============================================
// Cấu hình Pipeline
// =============================================
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "Product Management API v1");
        c.RoutePrefix = "swagger";
    });
}

app.UseRouting();

// Middleware xác thực JWT (dùng chung key với Auth Service BaiThucHanh2)
app.UseMiddleware<JwtValidationMiddleware>();

app.UseAuthorization();
app.MapControllers();

// Health check endpoint (public, không cần xác thực)
app.MapGet("/health", () => Results.Ok(new { Status = "Healthy", Service = "ProductService", Port = 5001 }));

app.Run();

using Microsoft.EntityFrameworkCore;
using ReportService.Data;
using ReportService.Services;
using ServiceDefaults;

var builder = WebApplication.CreateBuilder(args);
builder.AddServiceApi("Bai thuc hanh 5 - Reporting Service");

builder.Services.AddDbContext<ReportDbContext>(options =>
    options.UseSqlite(
        builder.Configuration.GetConnectionString("Reports") ?? "Data Source=reports.db"));

builder.Services.AddHttpClient<OrderClient>(client =>
{
    client.BaseAddress = new Uri(
        builder.Configuration["Services:Orders"] ?? "http://localhost:5002/");
    client.Timeout = TimeSpan.FromSeconds(5);
}).ConfigurePrimaryHttpMessageHandler(() =>
    new HttpClientHandler { AllowAutoRedirect = false });

builder.Services.AddScoped<ReportManager>();

var app = builder.Build();
using (var scope = app.Services.CreateScope())
    scope.ServiceProvider.GetRequiredService<ReportDbContext>().Database.EnsureCreated();

app.UseServiceApi();
app.Run();

public partial class Program;

using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.OpenApi.Models;

namespace ServiceDefaults;

public static class ServiceSetup
{
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
    };

    public static void AddServiceApi(this WebApplicationBuilder builder, string title)
    {
        builder.Services.AddControllers().AddJsonOptions(options =>
            options.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower);
        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddSwaggerGen(options =>
        {
            options.SwaggerDoc("v1", new OpenApiInfo { Title = title, Version = "v1" });
            options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
            {
                Type = SecuritySchemeType.Http, Scheme = "bearer", BearerFormat = "JWT"
            });
            options.AddSecurityRequirement(new OpenApiSecurityRequirement
            {
                [new OpenApiSecurityScheme
                {
                    Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
                }] = Array.Empty<string>()
            });
        });
        builder.Services.AddHttpClient("auth", client =>
        {
            client.BaseAddress = new Uri(builder.Configuration["Services:Auth"] ?? "http://localhost:5194/");
            client.Timeout = TimeSpan.FromSeconds(5);
        }).ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false });
    }

    public static void UseServiceApi(this WebApplication app)
    {
        app.Use(async (context, next) =>
        {
            try { await next(context); }
            catch (ApiException exception)
            {
                context.Response.StatusCode = exception.StatusCode;
                await context.Response.WriteAsJsonAsync(new { error = exception.Message });
            }
            catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException)
            {
                if (context.RequestAborted.IsCancellationRequested) return;
                context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
                await context.Response.WriteAsJsonAsync(new { error = "Khong the ket noi service phu thuoc. Vui long thu lai." });
            }
            catch (Exception exception)
            {
                app.Logger.LogError(exception, "Request failed");
                context.Response.StatusCode = StatusCodes.Status500InternalServerError;
                await context.Response.WriteAsJsonAsync(new { error = "Loi xu ly du lieu." });
            }
        });
        if (app.Environment.IsDevelopment())
        {
            app.UseSwagger();
            app.UseSwaggerUI();
        }
        app.UseMiddleware<RemoteAuthMiddleware>();
        app.MapControllers();
    }
}

public sealed class ApiException(int statusCode, string message) : Exception(message)
{
    public int StatusCode { get; } = statusCode;
}

// HTTP contract only: services never share database entities.
public sealed record ProductSnapshot(int Id, string Name, decimal Price, int Quantity);

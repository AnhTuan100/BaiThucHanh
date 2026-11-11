using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace ProductService.Middlewares;

/// <summary>
/// Middleware xác thực JWT Token cho tất cả các route /products.
/// Token được phát hành bởi service xác thực (BaiThucHanh2 - cổng 5194).
/// </summary>
public class JwtValidationMiddleware
{
    private readonly RequestDelegate _next;
    private readonly string _secretKey;
    private readonly string _issuer;
    private readonly string _audience;

    // Các path được bỏ qua xác thực (public endpoints)
    private static readonly HashSet<string> PublicPaths = new(StringComparer.OrdinalIgnoreCase)
    {
        "/swagger",
        "/health"
    };

    public JwtValidationMiddleware(RequestDelegate next, IConfiguration configuration)
    {
        _next      = next;
        _secretKey = configuration["Jwt:Key"]      ?? "KhoaBiMatChoBaiThucHanhSo2Rider2026RatBaoMat!";
        _issuer    = configuration["Jwt:Issuer"]   ?? "BaiThucHanh2";
        _audience  = configuration["Jwt:Audience"] ?? "ClientApp";
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var path = context.Request.Path.Value ?? string.Empty;

        // Bỏ qua Swagger UI và health check
        if (IsPublicPath(path))
        {
            await _next(context);
            return;
        }

        // Lấy Authorization header
        var authHeader = context.Request.Headers["Authorization"].FirstOrDefault();

        if (string.IsNullOrEmpty(authHeader) || !authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            context.Response.StatusCode  = StatusCodes.Status401Unauthorized;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync("{\"error\": \"401 Unauthorized: Thiếu hoặc sai định dạng Bearer token\"}");
            return;
        }

        var token     = authHeader["Bearer ".Length..].Trim();
        var principal = ValidateToken(token);

        if (principal == null)
        {
            context.Response.StatusCode  = StatusCodes.Status401Unauthorized;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync("{\"error\": \"401 Unauthorized: Token không hợp lệ hoặc đã hết hạn\"}");
            return;
        }

        // Gán Claims vào context để Controller có thể đọc
        context.User = principal;
        await _next(context);
    }

    private bool IsPublicPath(string path)
    {
        foreach (var pub in PublicPaths)
            if (path.StartsWith(pub, StringComparison.OrdinalIgnoreCase))
                return true;
        return false;
    }

    private ClaimsPrincipal? ValidateToken(string token)
    {
        var tokenHandler = new JwtSecurityTokenHandler();
        tokenHandler.InboundClaimTypeMap.Clear();
        var key = Encoding.UTF8.GetBytes(_secretKey);

        try
        {
            return tokenHandler.ValidateToken(token, new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey        = new SymmetricSecurityKey(key),
                ValidateIssuer          = true,
                ValidIssuer             = _issuer,
                ValidateAudience        = true,
                ValidAudience           = _audience,
                ValidateLifetime        = true,
                ClockSkew               = TimeSpan.Zero,
                NameClaimType           = "UserName"
            }, out _);
        }
        catch
        {
            return null;
        }
    }
}

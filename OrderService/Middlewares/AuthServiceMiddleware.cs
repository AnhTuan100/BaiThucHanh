using System.Net;
using System.Net.Http.Headers;
using OrderService.Services;

namespace OrderService.Middlewares;

// Xác thực trực tiếp qua /auth của bài 2, không truy cập database hoặc sao chép khóa JWT.
public sealed class AuthServiceMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context, IHttpClientFactory factory)
    {
        if (!context.Request.Path.StartsWithSegments("/orders", StringComparison.OrdinalIgnoreCase) &&
            !context.Request.Path.StartsWithSegments("/order_items", StringComparison.OrdinalIgnoreCase))
        {
            await next(context);
            return;
        }

        if (!AuthenticationHeaderValue.TryParse(context.Request.Headers.Authorization, out var header) ||
            !header.Scheme.Equals("Bearer", StringComparison.OrdinalIgnoreCase) || string.IsNullOrWhiteSpace(header.Parameter))
            throw new ApiException(401, "Thiếu hoặc sai định dạng Bearer token.");

        using var request = new HttpRequestMessage(HttpMethod.Get, "auth");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", header.Parameter);
        // Chuẩn hóa scheme khi chuyển tiếp sang Product Service.
        context.Request.Headers.Authorization = request.Headers.Authorization.ToString();
        try
        {
            using var response = await factory.CreateClient("AuthService").SendAsync(request, context.RequestAborted);
            if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
                throw new ApiException(401, "Token không hợp lệ hoặc đã hết hạn.");
            if (!response.IsSuccessStatusCode)
                throw new ApiException(503, "Auth Service đang gặp lỗi. Vui lòng thử lại.");
        }
        catch (HttpRequestException)
        {
            throw new ApiException(503, "Không kết nối được Auth Service.");
        }
        catch (OperationCanceledException) when (!context.RequestAborted.IsCancellationRequested)
        {
            throw new ApiException(503, "Auth Service phản hồi quá thời gian chờ.");
        }

        await next(context);
    }
}

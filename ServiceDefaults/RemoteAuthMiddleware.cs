using System.Net;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Http;

namespace ServiceDefaults;

public sealed class RemoteAuthMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context, IHttpClientFactory clients)
    {
        if (!AuthenticationHeaderValue.TryParse(context.Request.Headers.Authorization, out var header)
            || !header.Scheme.Equals("Bearer", StringComparison.OrdinalIgnoreCase)
            || string.IsNullOrWhiteSpace(header.Parameter))
            throw new ApiException(401, "Thieu hoac sai dinh dang Bearer token.");

        using var request = new HttpRequestMessage(HttpMethod.Get, "auth");
        request.Headers.Authorization = header;
        using var response = await clients.CreateClient("auth").SendAsync(request, context.RequestAborted);
        if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
            throw new ApiException(401, "Token khong hop le hoac het han.");
        if (!response.IsSuccessStatusCode)
            throw new ApiException(503, "Service xac thuc khong san sang.");

        using var body = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(context.RequestAborted),
            cancellationToken: context.RequestAborted);
        var userName = body.RootElement.GetProperty("userName").GetString();
        var userId = body.RootElement.GetProperty("idUser").GetString();
        if (string.IsNullOrWhiteSpace(userName) || string.IsNullOrWhiteSpace(userId))
            throw new ApiException(503, "Phan hoi xac thuc khong hop le.");
        context.User = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(ClaimTypes.Name, userName), new Claim("IdUser", userId)], "RemoteAuth"));
        await next(context);
    }
}

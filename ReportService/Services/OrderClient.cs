using System.Net;
using System.Net.Http.Headers;
using ServiceDefaults;

namespace ReportService.Services;

// ─── Snapshot hợp đồng HTTP của OrderService ───────────────────────────────

public sealed record ApiResponse<T>(bool Success, T? Data, string? Message);

public sealed record OrderItemSnapshot(
    int Id, int OrderId, int ProductId,
    string ProductName, int Quantity,
    decimal UnitPrice, decimal TotalPrice);

public sealed record OrderSnapshot(
    int Id, string CustomerName, string CustomerEmail,
    decimal TotalAmount, string Status,
    DateTime CreatedAt, DateTime UpdatedAt,
    List<OrderItemSnapshot> Items);

// ─── Client gọi OrderService ────────────────────────────────────────────────

public sealed class OrderClient(HttpClient client)
{
    public async Task<OrderSnapshot> GetOrderAsync(int id, string token, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, $"orders/{id}");
        request.Headers.Authorization = AuthenticationHeaderValue.Parse(token);
        using var response = await client.SendAsync(request, ct);

        if (response.StatusCode == HttpStatusCode.NotFound)
            throw new ApiException(404, $"Không tìm thấy đơn hàng {id}.");
        if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
            throw new ApiException(401, "OrderService từ chối token.");
        if (!response.IsSuccessStatusCode)
            throw new ApiException(503, "OrderService không sẵn sàng.");

        var body = await response.Content.ReadFromJsonAsync<ApiResponse<OrderSnapshot>>(ServiceSetup.Json, ct);
        if (body?.Data is null)
            throw new ApiException(502, "Phản hồi đơn hàng không hợp lệ.");
        return body.Data;
    }

    /// <summary>Lấy danh sách tất cả đơn hàng để tổng hợp báo cáo theo sản phẩm.</summary>
    public async Task<List<OrderSnapshot>> GetAllOrdersAsync(string token, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "orders");
        request.Headers.Authorization = AuthenticationHeaderValue.Parse(token);
        using var response = await client.SendAsync(request, ct);

        if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
            throw new ApiException(401, "OrderService từ chối token.");
        if (!response.IsSuccessStatusCode)
            throw new ApiException(503, "OrderService không sẵn sàng.");

        var body = await response.Content.ReadFromJsonAsync<ApiResponse<List<OrderSnapshot>>>(ServiceSetup.Json, ct);
        return body?.Data ?? [];
    }
}

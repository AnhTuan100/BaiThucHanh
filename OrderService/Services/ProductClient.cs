using System.Net;
using System.Net.Http.Headers;

namespace OrderService.Services;

public sealed class ProductClient(HttpClient client, IHttpContextAccessor accessor)
{
    public async Task<ProductSnapshot> GetAsync(int id, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, $"products/{id}");
        request.Headers.Authorization = AuthenticationHeaderValue.Parse(
            accessor.HttpContext!.Request.Headers.Authorization.ToString());
        try
        {
            using var response = await client.SendAsync(request, ct);
            if (response.StatusCode == HttpStatusCode.NotFound)
                throw new ApiException(400, $"Không tìm thấy sản phẩm ID = {id}.");
            if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
                throw new ApiException(401, "Product Service từ chối token xác thực.");
            if (!response.IsSuccessStatusCode)
                throw new ApiException(503, "Product Service đang gặp lỗi. Vui lòng thử lại.");
            var body = await response.Content.ReadFromJsonAsync<ProductResponse>(ct);
            if (body is not { Success: true, Data: not null } || body.Data.Id != id ||
                string.IsNullOrWhiteSpace(body.Data.Name) || body.Data.Name.Length > 255 ||
                body.Data.Price < 0 || body.Data.Quantity < 0)
                throw new ApiException(502, "Product Service trả về dữ liệu không hợp lệ.");
            return body.Data;
        }
        catch (HttpRequestException)
        {
            throw new ApiException(503, "Không kết nối được Product Service.");
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            throw new ApiException(503, "Product Service phản hồi quá thời gian chờ.");
        }
        catch (System.Text.Json.JsonException)
        {
            throw new ApiException(502, "Product Service trả về dữ liệu không hợp lệ.");
        }
    }

    private sealed record ProductResponse(bool Success, ProductSnapshot? Data);
}

public sealed record ProductSnapshot(int Id, string Name, decimal Price, int Quantity);

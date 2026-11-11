using System.Net;
using System.Net.Http.Headers;
using ServiceDefaults;

namespace OrderService.Services;

public sealed class ProductClient(HttpClient client)
{
    public async Task<ProductSnapshot> GetAsync(int id, string token, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, $"products/{id}");
        request.Headers.Authorization = AuthenticationHeaderValue.Parse(token);
        using var response = await client.SendAsync(request, cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
            throw new ApiException(404, $"Khong tim thay san pham {id}.");
        if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
            throw new ApiException(401, "Service san pham tu choi token.");
        if (!response.IsSuccessStatusCode)
            throw new ApiException(503, "Service san pham khong san sang.");
        var product = await response.Content.ReadFromJsonAsync<ProductSnapshot>(ServiceSetup.Json, cancellationToken);
        if (product is null || product.Id != id || string.IsNullOrWhiteSpace(product.Name)
            || product.Name.Length > 255 || product.Price < 0 || product.Price > 99999999.99m
            || decimal.Round(product.Price, 2) != product.Price || product.Quantity < 0)
            throw new ApiException(502, "Du lieu san pham khong hop le.");
        return product;
    }
}

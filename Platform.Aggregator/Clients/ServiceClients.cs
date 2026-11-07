using System.Net;

namespace Platform.Aggregator.Clients;

internal static class HttpClientExtensions
{
    public static async Task<T?> GetOrNullAsync<T>(this HttpClient http, string url, CancellationToken ct)
    {
        using var response = await http.GetAsync(url, ct);
        if (response.StatusCode == HttpStatusCode.NotFound) return default;
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<T>(ct);
    }
}

public class OrdersClient(HttpClient http)
{
    public Task<OrderDto?> GetOrderAsync(long id, CancellationToken ct)
        => http.GetOrNullAsync<OrderDto>($"/api/orders/{id}", ct);
}

public class CatalogClient(HttpClient http)
{
    public Task<CatalogItemDto?> GetMenuItemAsync(long id, CancellationToken ct)
        => http.GetOrNullAsync<CatalogItemDto>($"/api/menu-items/{id}", ct);
}

public class ReviewsClient(HttpClient http)
{
    public Task<ReviewsPageDto?> GetLatestAsync(long menuItemId, int count, CancellationToken ct)
        => http.GetOrNullAsync<ReviewsPageDto>($"/api/reviews?menuItemId={menuItemId}&page=1&pageSize={count}", ct);
}
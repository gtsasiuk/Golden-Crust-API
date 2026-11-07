using System.Collections.Concurrent;
using System.Diagnostics;
using Platform.Aggregator.Clients;
using Platform.Aggregator.Models;

namespace Platform.Aggregator.Services;

public class UpstreamUnavailableException(string service, Exception inner)
    : Exception($"{service} is unavailable", inner);

public class OrderAggregationService(
    OrdersClient orders, CatalogClient catalog, ReviewsClient reviews,
    ILogger<OrderAggregationService> logger)
{
    public async Task<FullOrderDto?> GetFullOrderAsync(long orderId, CancellationToken ct)
    {
        OrderDto? order;
        try
        {
            logger.LogInformation("Calling orders-api for order {OrderId}", orderId);
            order = await orders.GetOrderAsync(orderId, ct);
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            throw new UpstreamUnavailableException("orders-api", ex);
        }
        if (order is null) return null;

        var warnings = new ConcurrentBag<string>();

        var lines = await Task.WhenAll(order.Items.Select(i => BuildLineAsync(i, warnings, ct)));

        return new FullOrderDto(
            new OrderInfo(order.Id, order.CustomerId, order.Status, order.PaymentStatus,
                          order.TotalAmount, order.OrderDate, order.DeliveryAddress),
            lines,
            warnings.Distinct().Order().ToList());
    }

    private async Task<OrderLineDto> BuildLineAsync(OrderItemDto item, ConcurrentBag<string> warnings, CancellationToken ct)
    {
        var menuTask = SafeAsync(c => catalog.GetMenuItemAsync(item.MenuItemId, c), "catalog-api", item.MenuItemId, warnings, ct);
        var reviewsTask = SafeAsync(c => reviews.GetLatestAsync(item.MenuItemId, 3, c), "reviews-api", item.MenuItemId, warnings, ct);

        await Task.WhenAll(menuTask, reviewsTask);
        var menu = await menuTask;
        var reviewsPage = await reviewsTask;

        if (menu is not null && menu.Id != item.MenuItemId)
        {
            logger.LogWarning("catalog-api returned menu item {ActualId} instead of {ExpectedId}", menu.Id, item.MenuItemId);
            warnings.Add("catalog-api returned inconsistent data");
            menu = null;
        }

        return new OrderLineDto(
            item.MenuItemId, item.ProductName, item.UnitPrice, item.Quantity, item.LineTotal,
            menu is null ? null : new MenuInfo(
                menu.Name, menu.CategoryName, menu.Price, menu.IsAvailable, menu.Description,
                menu.Images.FirstOrDefault(i => i.IsPrimary)?.Url,
                menu.Tags.Select(t => t.Name).ToList()),
            reviewsPage is null ? null : new ReviewsInfo(
                reviewsPage.TotalCount,
                reviewsPage.Items
                    .Where(r => r.MenuItemId == item.MenuItemId)
                    .Select(r => new ReviewInfo(r.Id, r.AuthorName, r.Rating, r.Text, r.Type, r.CreatedAt))
                    .ToList()));
    }

    private async Task<T?> SafeAsync<T>(
        Func<CancellationToken, Task<T?>> call, string service, long menuItemId,
        ConcurrentBag<string> warnings, CancellationToken ct)
    {
        var sw = Stopwatch.StartNew();
        logger.LogInformation("Calling {Service} for menu item {MenuItemId}", service, menuItemId);
        try
        {
            var result = await call(ct);
            logger.LogInformation("{Service} responded in {Elapsed} ms", service, sw.ElapsedMilliseconds);
            return result;
        }

        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            logger.LogWarning(ex, "{Service} is unavailable after {Elapsed} ms", service, sw.ElapsedMilliseconds);
            warnings.Add($"{service} is unavailable");
            return default;
        }
    }
}
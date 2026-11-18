using System.Collections.Concurrent;
using System.Diagnostics;
using Catalog.Contracts;
using Grpc.Core;
using Orders.Contracts;
using Platform.Aggregator.Models;
using Reviews.Contracts;

namespace Platform.Aggregator.Services;

public class UpstreamUnavailableException(string service, Exception inner)
    : Exception($"{service} is unavailable", inner);

public class OrderAggregationService(
    OrdersService.OrdersServiceClient orders,
    CatalogService.CatalogServiceClient catalog,
    ReviewsService.ReviewsServiceClient reviews,
    ILogger<OrderAggregationService> logger)
{
    private const int LatestReviewsCount = 3;

    private static readonly TimeSpan CallTimeout = TimeSpan.FromSeconds(5);
    private static DateTime Deadline() => DateTime.UtcNow.Add(CallTimeout);

    public async Task<FullOrderDto?> GetFullOrderAsync(long orderId, CancellationToken ct)
    {
        OrderReply order;
        try
        {
            logger.LogInformation("Calling orders-api via gRPC for order {OrderId}", orderId);
            order = await orders.GetOrderAsync(new GetOrderRequest { Id = orderId },
                deadline: Deadline(), cancellationToken: ct).ResponseAsync;
        }
        catch (RpcException ex) when (ex.StatusCode == StatusCode.NotFound)
        {
            return null;
        }
        catch (RpcException ex) when (ex.StatusCode == StatusCode.Cancelled && ct.IsCancellationRequested)
        {
            throw new OperationCanceledException(ct);
        }
        catch (RpcException ex)
        {
            throw new UpstreamUnavailableException("orders-api", ex);
        }

        var warnings = new ConcurrentBag<string>();
        var ids = order.Items.Select(i => i.MenuItemId).Distinct().ToList();
        if (ids.Count == 0)
            return Map(order, null, null, warnings);

        var menuRequest = new GetMenuItemsByIdsRequest { Ids = { ids } };
        var reviewsRequest = new GetLatestForMenuItemsRequest { MenuItemIds = { ids }, Count = LatestReviewsCount };

        var menuTask = SafeAsync(
            c => catalog.GetMenuItemsByIdsAsync(menuRequest, deadline: Deadline(), cancellationToken: c).ResponseAsync,
            "catalog-api", warnings, ct);
        var reviewsTask = SafeAsync(
            c => reviews.GetLatestForMenuItemsAsync(reviewsRequest, deadline: Deadline(), cancellationToken: c).ResponseAsync,
            "reviews-api", warnings, ct);

        await Task.WhenAll(menuTask, reviewsTask);

        return Map(order, await menuTask, await reviewsTask, warnings);
    }

    private async Task<T?> SafeAsync<T>(
        Func<CancellationToken, Task<T>> call, string service, ConcurrentBag<string> warnings, CancellationToken ct)
        where T : class
    {
        var sw = Stopwatch.StartNew();
        logger.LogInformation("Calling {Service} via gRPC", service);
        try
        {
            var result = await call(ct);
            logger.LogInformation("{Service} responded in {ElapsedMs} ms", service, sw.ElapsedMilliseconds);
            return result;
        }
        catch (RpcException ex) when (ex.StatusCode == StatusCode.Cancelled && ct.IsCancellationRequested)
        {
            throw new OperationCanceledException(ct);
        }
        catch (RpcException ex)
        {
            logger.LogWarning(ex, "{Service} gRPC call failed with {GrpcStatus} after {ElapsedMs} ms",
                service, ex.StatusCode, sw.ElapsedMilliseconds);
            warnings.Add(ex.StatusCode is StatusCode.Unavailable or StatusCode.DeadlineExceeded
                ? $"{service} is unavailable"
                : $"{service} returned an error");
            return null;
        }
    }

    private static FullOrderDto Map(
        OrderReply order, MenuItemsReply? menu, LatestReviewsReply? reviews, ConcurrentBag<string> warnings)
    {
        var menuById = menu?.Items.ToDictionary(m => m.Id);
        var reviewsById = reviews?.Items.ToDictionary(r => r.MenuItemId);

        var lines = new List<OrderLineDto>(order.Items.Count);
        foreach (var item in order.Items)
        {
            MenuItemReply? menuItem = null;
            menuById?.TryGetValue(item.MenuItemId, out menuItem);
            MenuItemReviews? itemReviews = null;
            reviewsById?.TryGetValue(item.MenuItemId, out itemReviews);

            lines.Add(new OrderLineDto(
                item.MenuItemId, item.ProductName, item.UnitPrice.ToDecimal(), item.Quantity, item.LineTotal.ToDecimal(),
                menuItem is null ? null : ToMenuInfo(menuItem),
                itemReviews is null ? null : ToReviewsInfo(itemReviews)));
        }

        return new FullOrderDto(
            new OrderInfo(order.Id, order.CustomerId, order.Status,
                          order.HasPaymentStatus ? order.PaymentStatus : null,
                          order.TotalAmount.ToDecimal(), order.OrderDate.ToDateTime(), order.DeliveryAddress),
            lines,
            warnings.Distinct().Order().ToList());
    }

    private static MenuInfo ToMenuInfo(MenuItemReply m) => new(
        m.Name, m.CategoryName, m.Price.ToDecimal(), m.IsAvailable,
        m.HasDescription ? m.Description : null,
        m.HasPrimaryImageUrl ? m.PrimaryImageUrl : null,
        m.Tags.ToList());

    private static ReviewsInfo ToReviewsInfo(MenuItemReviews r) => new(
        r.TotalCount,
        r.Latest.Select(x => new ReviewInfo(
            x.Id, x.AuthorName, x.Rating, x.HasText ? x.Text : null, x.Type, x.CreatedAt.ToDateTime())).ToList());
}
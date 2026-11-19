using System.Data;
using System.Diagnostics;
using AutoMapper;
using Microsoft.Extensions.Logging;
using Orders.Bll.Dtos;
using Orders.Bll.Metrics;
using Orders.Dal.Interfaces;
using Orders.Domain;

namespace Orders.Bll.Services;

public class OrderService(IUnitOfWork uow, IMapper mapper, ILogger<OrderService> logger, IOrderMetrics metrics)
    : IOrderService
{
    private static readonly ActivitySource Source = new("GoldenCrust.Orders");

    private static readonly string[] KitchenStatuses =
        [OrderStatus.Cooking, OrderStatus.ReadyForDelivery, OrderStatus.Delivering, OrderStatus.Delivered];

    public Task<OrderDto> CreateAsync(CreateOrderDto dto, CancellationToken ct = default)
        => MeasureAsync("create", () => CreateCoreAsync(dto, ct));

    public Task CheckoutAsync(long orderId, bool simulateFailure = false, CancellationToken ct = default)
        => MeasureAsync("checkout", () => CheckoutCoreAsync(orderId, simulateFailure, ct));

    public Task ChangeStatusAsync(long orderId, ChangeOrderStatusDto dto, CancellationToken ct = default)
        => MeasureAsync("change_status", () => ChangeStatusCoreAsync(orderId, dto, ct));

    public Task CancelAsync(long orderId, string? reason = null, CancellationToken ct = default)
        => MeasureAsync("cancel", () => CancelCoreAsync(orderId, reason, ct));

    public async Task<OrderDto> GetByIdAsync(long id, CancellationToken ct = default)
    {
        var order = await uow.Orders.GetWithItemsAsync(id, ct)
            ?? throw new NotFoundException($"Order {id} not found");
        var payment = await uow.Payments.GetActiveByOrderIdAsync(id, ct);
        return mapper.Map<OrderDto>(order) with { PaymentStatus = payment?.Status };
    }

    public async Task<IReadOnlyList<OrderSummaryDto>> GetByCustomerAsync(
        long customerId, string? status, int pageNumber, int pageSize, CancellationToken ct = default)
    {
        if (pageNumber < 1) throw new ValidationException("pageNumber must be >= 1");
        if (pageSize is < 1 or > 100) throw new ValidationException("pageSize must be between 1 and 100");
        if (status is not null && !OrderStatus.All.Contains(status))
            throw new ValidationException($"Unknown status '{status}'");
        if (await uow.Customers.GetByIdAsync(customerId, ct) is null)
            throw new NotFoundException($"Customer {customerId} not found");

        var rows = await uow.Orders.GetByCustomerAsync(customerId, status, pageNumber, pageSize, ct);
        return mapper.Map<IReadOnlyList<OrderSummaryDto>>(rows);
    }

    private async Task<OrderDto> CreateCoreAsync(CreateOrderDto dto, CancellationToken ct)
    {
        using var activity = Source.StartActivity("CreateOrder");
        activity?.SetTag("customer.id", dto.CustomerId);

        if (dto.DeliveryLat.HasValue != dto.DeliveryLng.HasValue)
            throw new ValidationException("DeliveryLat and DeliveryLng must be provided together");

        var requested = dto.Items
            .GroupBy(i => i.MenuItemId)
            .ToDictionary(g => g.Key, g => g.Sum(i => i.Quantity));

        long orderId = 0;
        await InTransactionAsync(async () =>
        {
            if (await uow.Customers.GetByIdAsync(dto.CustomerId, ct) is null)
                throw new NotFoundException($"Customer {dto.CustomerId} not found");

            var menu = (await uow.MenuItems.GetByIdsAsync(requested.Keys, ct)).ToDictionary(m => m.Id);
            var bad = requested.Keys.Where(id => !menu.TryGetValue(id, out var m) || !m.IsAvailable).ToList();
            if (bad.Count > 0)
                throw new ValidationException($"Menu items unknown or unavailable: {string.Join(", ", bad)}");

            var items = requested.Select(r => new OrderItem
            {
                MenuItemId = r.Key,
                ProductName = menu[r.Key].Name,
                UnitPrice = menu[r.Key].Price,
                Quantity = r.Value
            }).ToList();
            var total = items.Sum(i => i.UnitPrice * i.Quantity);

            orderId = await uow.Orders.AddAsync(new Order
            {
                CustomerId = dto.CustomerId,
                Status = OrderStatus.Placed,
                TotalAmount = total,
                DeliveryAddress = dto.DeliveryAddress.Trim(),
                DeliveryLat = dto.DeliveryLat,
                DeliveryLng = dto.DeliveryLng,
                Comment = dto.Comment
            }, ct);

            foreach (var item in items) item.OrderId = orderId;
            await uow.Orders.AddItemsAsync(items, ct);
            await uow.Payments.AddAsync(new Payment { OrderId = orderId, Amount = total, Status = PaymentStatus.Pending }, ct);
            await uow.Orders.AddHistoryAsync(new OrderStatusHistoryEntry { OrderId = orderId, ToStatus = OrderStatus.Placed }, ct);
        }, ct);

        var created = await GetByIdAsync(orderId, ct);
        metrics.OrderCreated(created.Status);
        activity?.SetTag("order.id", orderId);
        logger.LogInformation(
            "Order {OrderId} created for customer {CustomerId}: {ItemsCount} items, amount {Amount}, status {Status}",
            orderId, dto.CustomerId, created.Items.Count, created.TotalAmount, created.Status);
        return created;
    }

    private async Task CheckoutCoreAsync(long orderId, bool simulateFailure, CancellationToken ct)
    {
        using var activity = Source.StartActivity("ConfirmOrder");
        activity?.SetTag("order.id", orderId);

        await InTransactionAsync(async () =>
        {
            var order = await uow.Orders.GetWithItemsAsync(orderId, ct)
                ?? throw new NotFoundException($"Order {orderId} not found");
            if (order.Items.Count == 0)
                throw new BusinessConflictException($"Order {orderId} has no items");

            await uow.Payments.PayAsync(orderId, null, ct);

            if (simulateFailure)
                throw new BusinessConflictException("Simulated failure after payment: the transaction is rolled back");

            await uow.Orders.ConfirmAsync(orderId, ct);
        }, ct);

        metrics.OrderConfirmed();
        logger.LogInformation("Order {OrderId} paid and confirmed, status {Status}", orderId, OrderStatus.Accepted);
    }

    private async Task ChangeStatusCoreAsync(long orderId, ChangeOrderStatusDto dto, CancellationToken ct)
    {
        if (!KitchenStatuses.Contains(dto.NewStatus))
            throw new ValidationException($"NewStatus must be one of: {string.Join(", ", KitchenStatuses)}");

        await uow.Orders.ChangeStatusAsync(orderId, dto.NewStatus, dto.Note, ct);
        metrics.OrderStatusChanged(dto.NewStatus);
        logger.LogInformation("Order {OrderId} status changed to {Status}", orderId, dto.NewStatus);
    }

    private async Task CancelCoreAsync(long orderId, string? reason, CancellationToken ct)
    {
        await uow.Orders.CancelAsync(orderId, reason, ct);
        metrics.OrderCancelled();
        logger.LogInformation("Order {OrderId} cancelled", orderId);
    }

    private async Task<T> MeasureAsync<T>(string operation, Func<Task<T>> action)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            var result = await action();
            metrics.RecordDuration(operation, true, sw.Elapsed.TotalMilliseconds);
            return result;
        }
        catch
        {
            metrics.RecordDuration(operation, false, sw.Elapsed.TotalMilliseconds);
            throw;
        }
    }

    private async Task MeasureAsync(string operation, Func<Task> action)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            await action();
            metrics.RecordDuration(operation, true, sw.Elapsed.TotalMilliseconds);
        }
        catch
        {
            metrics.RecordDuration(operation, false, sw.Elapsed.TotalMilliseconds);
            throw;
        }
    }

    private async Task InTransactionAsync(Func<Task> work, CancellationToken ct)
    {
        await uow.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct);
        try
        {
            await work();
            await uow.CommitAsync(ct);
        }
        catch
        {
            await uow.RollbackAsync(CancellationToken.None);
            throw;
        }
    }
}
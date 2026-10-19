using System.Data;
using AutoMapper;
using Microsoft.Extensions.Logging;
using Orders.Bll.Dtos;
using Orders.Dal.Interfaces;
using Orders.Domain;

namespace Orders.Bll.Services;

public class OrderService(IUnitOfWork uow, IMapper mapper, ILogger<OrderService> logger) : IOrderService
{
    // Статуси, які можна виставляти через /status (кухня й доставка).
    // Placed→Accepted робить checkout, Cancelled робить /cancel.
    private static readonly string[] KitchenStatuses =
        [OrderStatus.Cooking, OrderStatus.ReadyForDelivery, OrderStatus.Delivering, OrderStatus.Delivered];

    // ---------- Транзакційна операція 1: створення замовлення ----------
    // Orders + OrderDetails + OrderItems (ЗНІМОК назви/ціни) + Payment + історія: усе або нічого.
    public async Task<OrderDto> CreateAsync(CreateOrderDto dto, CancellationToken ct = default)
    {
        // Бізнес-валідація, якій не потрібна БД
        if (dto.DeliveryLat.HasValue != dto.DeliveryLng.HasValue)
            throw new ValidationException("DeliveryLat and DeliveryLng must be provided together");

        // Дублікати однієї страви в запиті складаємо (так само робила usp_CreateOrder)
        var requested = dto.Items
            .GroupBy(i => i.MenuItemId)
            .ToDictionary(g => g.Key, g => g.Sum(i => i.Quantity));

        long orderId = 0;
        await InTransactionAsync(async () =>
        {
            // Бізнес-валідація зі станом БД
            if (await uow.Customers.GetByIdAsync(dto.CustomerId, ct) is null)
                throw new NotFoundException($"Customer {dto.CustomerId} not found");

            var menu = (await uow.MenuItems.GetByIdsAsync(requested.Keys, ct)).ToDictionary(m => m.Id);
            var bad = requested.Keys.Where(id => !menu.TryGetValue(id, out var m) || !m.IsAvailable).ToList();
            if (bad.Count > 0)
                throw new ValidationException($"Menu items unknown or unavailable: {string.Join(", ", bad)}");

            // ЗНІМОК: назва й ціна копіюються з довідника і далі не змінюються
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

        logger.LogInformation("Order {OrderId} created for customer {CustomerId}", orderId, dto.CustomerId);
        return await GetByIdAsync(orderId, ct);
    }

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

    // ---------- Транзакційна операція 2: оплата + підтвердження ----------
    // Payments (usp_PayOrder) і Orders (usp_ConfirmOrder): два репозиторії, одна транзакція.
    // Допустимість переходу перевіряють самі процедури: недопустимий перехід → 409.
    public async Task CheckoutAsync(long orderId, bool simulateFailure = false, CancellationToken ct = default)
    {
        await InTransactionAsync(async () =>
        {
            var order = await uow.Orders.GetWithItemsAsync(orderId, ct)
                ?? throw new NotFoundException($"Order {orderId} not found");
            if (order.Items.Count == 0)
                throw new BusinessConflictException($"Order {orderId} has no items");

            await uow.Payments.PayAsync(orderId, null, ct);      // Pending → Paid

            if (simulateFailure)                                 // демо відкату: платіж уже "Paid", але далі помилка
                throw new BusinessConflictException("Simulated failure after payment: the transaction is rolled back");

            await uow.Orders.ConfirmAsync(orderId, ct);          // Placed → Accepted
        }, ct);

        logger.LogInformation("Order {OrderId} paid and confirmed", orderId);
    }

    public async Task ChangeStatusAsync(long orderId, ChangeOrderStatusDto dto, CancellationToken ct = default)
    {
        if (!KitchenStatuses.Contains(dto.NewStatus))
            throw new ValidationException($"NewStatus must be one of: {string.Join(", ", KitchenStatuses)}");

        await uow.Orders.ChangeStatusAsync(orderId, dto.NewStatus, dto.Note, ct);   // usp_ChangeStatus (сама транзакційна)
        logger.LogInformation("Order {OrderId} status changed to {Status}", orderId, dto.NewStatus);
    }

    public async Task CancelAsync(long orderId, string? reason = null, CancellationToken ct = default)
    {
        await uow.Orders.CancelAsync(orderId, reason, ct);                          // usp_CancelOrder (сама транзакційна)
        logger.LogInformation("Order {OrderId} cancelled", orderId);
    }

    // Шаблон "відкрити транзакцію → виконати → Commit, а при помилці Rollback і прокинути виняток далі"
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
            await uow.RollbackAsync(CancellationToken.None);   // відкат не має залежати від скасованого запиту
            throw;
        }
    }
}
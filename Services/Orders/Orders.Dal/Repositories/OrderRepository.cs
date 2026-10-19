using Dapper;
using Microsoft.Data.SqlClient;
using Orders.Dal.Interfaces;
using Orders.Domain;

namespace Orders.Dal.Repositories;

public sealed class OrderRepository : BaseDapperRepository<Order>, IOrderRepository
{
    public OrderRepository(SqlConnection c, Func<SqlTransaction?> tx) : base(c, tx, "dbo.Orders") { }

    // Order у моделі містить і поля OrderDetails (1:1), тому читаємо через JOIN.
    // У WHERE/ORDER BY "Id" і "IsDeleted" однозначні: в OrderDetails таких стовпців немає.
    protected override string FromClause => "dbo.Orders o JOIN dbo.OrderDetails d ON d.OrderId = o.Id";
    protected override string SelectColumns =>
        "o.Id, o.CustomerId, o.Status, o.TotalAmount, o.OrderDate, " +
        "d.DeliveryAddress, d.DeliveryLat, d.DeliveryLng, d.Comment";

    // Orders + OrderDetails одним батчем. Змінну названо @NewId, а не @Id: Dapper візьме з Order
    // властивість Id для кожного @Id у тексті, і вийде конфлікт з DECLARE.
    protected override string InsertSql => @"
DECLARE @NewId BIGINT;
INSERT INTO dbo.Orders (CustomerId, Status, TotalAmount, CreatedBy)
VALUES (@CustomerId, @Status, @TotalAmount, N'api');
SET @NewId = SCOPE_IDENTITY();
INSERT INTO dbo.OrderDetails (OrderId, DeliveryAddress, DeliveryLat, DeliveryLng, Comment)
VALUES (@NewId, @DeliveryAddress, @DeliveryLat, @DeliveryLng, @Comment);
SELECT @NewId;";

    public Task<Order?> GetWithItemsAsync(long orderId, CancellationToken ct = default) =>
        RunAsync<Order?>(async () =>
        {
            const string sql = @"
SELECT o.Id, o.CustomerId, o.Status, o.TotalAmount, o.OrderDate,
       d.DeliveryAddress, d.DeliveryLat, d.DeliveryLng, d.Comment,
       i.OrderId, i.MenuItemId, i.ProductName, i.UnitPrice, i.Quantity, i.LineTotal
FROM dbo.Orders o
JOIN dbo.OrderDetails d ON d.OrderId = o.Id
LEFT JOIN dbo.OrderItems i ON i.OrderId = o.Id
WHERE o.Id = @OrderId AND o.IsDeleted = 0
ORDER BY i.MenuItemId";

            await EnsureOpenAsync(ct);
            Order? result = null;
            await Connection.QueryAsync<Order, OrderItem?, Order>(
                new CommandDefinition(sql, new { OrderId = orderId }, Transaction, cancellationToken: ct),
                (order, item) =>                       // кожен рядок JOIN: Order + одна позиція
                {
                    result ??= order;
                    if (item is not null) result.Items.Add(item);
                    return result;
                },
                splitOn: "OrderId");                   // з цього стовпця починається OrderItem
            return result;
        });

    public Task AddItemsAsync(IEnumerable<OrderItem> items, CancellationToken ct = default) =>
        RunAsync(async () =>
        {
            const string sql = @"INSERT INTO dbo.OrderItems (OrderId, MenuItemId, ProductName, UnitPrice, Quantity)
                                 VALUES (@OrderId, @MenuItemId, @ProductName, @UnitPrice, @Quantity)";
            await EnsureOpenAsync(ct);
            // колекція як параметр: Dapper виконає команду для кожного елемента
            await Connection.ExecuteAsync(new CommandDefinition(sql, items, Transaction, cancellationToken: ct));
        });

    public Task AddHistoryAsync(OrderStatusHistoryEntry entry, CancellationToken ct = default) =>
        RunAsync(async () =>
        {
            const string sql = @"INSERT INTO dbo.OrderStatusHistory (OrderId, FromStatus, ToStatus, ChangedBy, Note)
                                 VALUES (@OrderId, @FromStatus, @ToStatus, @ChangedBy, @Note)";
            await EnsureOpenAsync(ct);
            await Connection.ExecuteAsync(new CommandDefinition(sql,
                new { entry.OrderId, entry.FromStatus, entry.ToStatus, ChangedBy = entry.ChangedBy ?? Actor, entry.Note },
                Transaction, cancellationToken: ct));
        });

    public Task<IReadOnlyList<OrderSummary>> GetByCustomerAsync(
        long customerId, string? status, int pageNumber, int pageSize, CancellationToken ct = default) =>
        RunAsync<IReadOnlyList<OrderSummary>>(async () =>
        {
            await EnsureOpenAsync(ct);
            var rows = await Connection.QueryAsync<OrderSummary>(new CommandDefinition(
                "dbo.usp_GetCustomerOrders",
                new { CustomerId = customerId, Status = status, PageNumber = pageNumber, PageSize = pageSize },
                Transaction, commandType: System.Data.CommandType.StoredProcedure, cancellationToken: ct));
            return rows.ToList();
        });

    public Task ConfirmAsync(long orderId, CancellationToken ct = default) =>
        CallProcedureAsync("dbo.usp_ConfirmOrder", new { OrderId = orderId, ChangedBy = Actor }, ct);

    public Task ChangeStatusAsync(long orderId, string newStatus, string? note = null, CancellationToken ct = default) =>
        CallProcedureAsync("dbo.usp_ChangeStatus",
            new { OrderId = orderId, NewStatus = newStatus, ChangedBy = Actor, Note = note }, ct);

    public Task CancelAsync(long orderId, string? reason = null, CancellationToken ct = default) =>
        CallProcedureAsync("dbo.usp_CancelOrder", new { OrderId = orderId, Reason = reason, ChangedBy = Actor }, ct);
}
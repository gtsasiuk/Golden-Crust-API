/* =====================================================================
   Golden Crust | контекст ORDERS | Крок 2/3: збережувані процедури (6 шт.)
   CREATE OR ALTER: скрипт можна запускати повторно.
   Коди помилок (THROW):
     50001 Order not found                 50010 ItemsJson is not valid JSON
     50002 Only a Placed order can be confirmed   50011 Order must contain items
     50003 Invalid status transition       50012 Item needs menuItemId and quantity > 0
     50004 Order is not paid yet           50013 Customer not found
     50005 Only a Placed order can be paid 50014 Menu item unknown/unavailable
     50006 No pending payment              50020 Invalid paging parameters
     50007 Cancellation allowed only before cooking
   Дозволені переходи статусів:
     Placed -> Accepted            (ТІЛЬКИ usp_ConfirmOrder, потрібна оплата)
     Accepted -> Cooking -> ReadyForDelivery -> Delivering -> Delivered (usp_ChangeStatus)
     Placed | Accepted -> Cancelled (ТІЛЬКИ usp_CancelOrder, до початку приготування)
   ===================================================================== */
USE GoldenCrust_Orders;
GO
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
SET ARITHABORT ON;
GO

/* ---------------------------------------------------------------------
   1. usp_CreateOrder: створює замовлення атомарно:
      Orders + OrderDetails (1:1) + OrderItems (знімок назви/ціни) +
      Payment (Pending) + перший запис в історії статусів.
   @ItemsJson: [{"menuItemId":1,"quantity":2}, {"menuItemId":5,"quantity":1}]
   (JSON замість table-valued parameter: простіше викликати з Dapper)
   --------------------------------------------------------------------- */
CREATE OR ALTER PROCEDURE dbo.usp_CreateOrder
    @CustomerId      BIGINT,
    @DeliveryAddress NVARCHAR(400),
    @ItemsJson       NVARCHAR(MAX),
    @DeliveryLat     DECIMAL(9,6)   = NULL,
    @DeliveryLng     DECIMAL(9,6)   = NULL,
    @Comment         NVARCHAR(1000) = NULL,
    @CreatedBy       NVARCHAR(100)  = NULL,
    @OrderId         BIGINT         OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    IF ISNULL(ISJSON(@ItemsJson), 0) = 0
        THROW 50010, 'ItemsJson must be a valid JSON array', 1;

    DECLARE @Items TABLE (MenuItemId BIGINT NULL, Quantity INT NULL);
    INSERT INTO @Items (MenuItemId, Quantity)
    SELECT MenuItemId, Quantity
    FROM OPENJSON(@ItemsJson)
         WITH (MenuItemId BIGINT '$.menuItemId', Quantity INT '$.quantity');

    IF NOT EXISTS (SELECT 1 FROM @Items)
        THROW 50011, 'Order must contain at least one item', 1;
    IF EXISTS (SELECT 1 FROM @Items WHERE MenuItemId IS NULL OR Quantity IS NULL OR Quantity <= 0)
        THROW 50012, 'Every item needs menuItemId and quantity > 0', 1;
    IF NOT EXISTS (SELECT 1 FROM dbo.Customers WHERE Id = @CustomerId AND IsDeleted = 0)
        THROW 50013, 'Customer not found', 1;
    IF EXISTS (SELECT 1
               FROM @Items i
               LEFT JOIN dbo.MenuItems m
                      ON m.Id = i.MenuItemId AND m.IsDeleted = 0 AND m.IsAvailable = 1
               WHERE m.Id IS NULL)
        THROW 50014, 'One or more menu items are unknown or unavailable', 1;

    BEGIN TRANSACTION;

    INSERT INTO dbo.Orders (CustomerId, Status, TotalAmount, CreatedBy)
    VALUES (@CustomerId, 'Placed', 0, @CreatedBy);
    SET @OrderId = SCOPE_IDENTITY();

    INSERT INTO dbo.OrderDetails (OrderId, DeliveryAddress, DeliveryLat, DeliveryLng, Comment)
    VALUES (@OrderId, @DeliveryAddress, @DeliveryLat, @DeliveryLng, @Comment);

    -- ЗНІМОК: назва й ціна копіюються з локальної копії довідника і далі не змінюються
    INSERT INTO dbo.OrderItems (OrderId, MenuItemId, ProductName, UnitPrice, Quantity)
    SELECT @OrderId, i.MenuItemId, m.Name, m.Price, SUM(i.Quantity)
    FROM @Items i
    JOIN dbo.MenuItems m ON m.Id = i.MenuItemId
    GROUP BY i.MenuItemId, m.Name, m.Price;   -- дублікати однієї страви в JSON складаються

    DECLARE @Total DECIMAL(10,2) =
        (SELECT SUM(LineTotal) FROM dbo.OrderItems WHERE OrderId = @OrderId);

    UPDATE dbo.Orders SET TotalAmount = @Total WHERE Id = @OrderId;

    INSERT INTO dbo.Payments (OrderId, Amount, Status, CreatedBy)
    VALUES (@OrderId, @Total, 'Pending', @CreatedBy);

    INSERT INTO dbo.OrderStatusHistory (OrderId, FromStatus, ToStatus, ChangedBy)
    VALUES (@OrderId, NULL, 'Placed', @CreatedBy);

    COMMIT TRANSACTION;
END
GO

/* ---------------------------------------------------------------------
   2. usp_PayOrder: ІМІТАЦІЯ оплати (без платіжного шлюзу).
      Pending -> Paid. Статус замовлення не змінює (це робить usp_ConfirmOrder).
   --------------------------------------------------------------------- */
CREATE OR ALTER PROCEDURE dbo.usp_PayOrder
    @OrderId        BIGINT,
    @TransactionRef NVARCHAR(100) = NULL,
    @ChangedBy      NVARCHAR(100) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    BEGIN TRANSACTION;

    DECLARE @Status NVARCHAR(30) =
        (SELECT Status FROM dbo.Orders WITH (UPDLOCK, ROWLOCK) WHERE Id = @OrderId AND IsDeleted = 0);

    IF @Status IS NULL
        THROW 50001, 'Order not found', 1;
    IF @Status <> 'Placed'
        THROW 50005, 'Only a Placed order can be paid', 1;

    UPDATE dbo.Payments
    SET Status         = 'Paid',
        PaidAt         = SYSUTCDATETIME(),
        TransactionRef = COALESCE(@TransactionRef, CONVERT(NVARCHAR(36), NEWID())),
        UpdatedAt      = SYSUTCDATETIME(),
        UpdatedBy      = @ChangedBy
    WHERE OrderId = @OrderId AND Status = 'Pending';

    IF @@ROWCOUNT = 0
        THROW 50006, 'No pending payment for this order (already paid?)', 1;

    COMMIT TRANSACTION;
END
GO

/* ---------------------------------------------------------------------
   3. usp_ConfirmOrder (ТРАНЗАКЦІЙНА): Placed -> Accepted.
      Відмовляє, якщо замовлення не в статусі Placed або не оплачене.
      UPDLOCK тримає рядок до кінця транзакції (як SELECT ... FOR UPDATE в JPA).
   --------------------------------------------------------------------- */
CREATE OR ALTER PROCEDURE dbo.usp_ConfirmOrder
    @OrderId   BIGINT,
    @ChangedBy NVARCHAR(100) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;   -- будь-яка помилка відкочує транзакцію

    BEGIN TRANSACTION;

    DECLARE @Status NVARCHAR(30) =
        (SELECT Status FROM dbo.Orders WITH (UPDLOCK, ROWLOCK) WHERE Id = @OrderId AND IsDeleted = 0);

    IF @Status IS NULL
        THROW 50001, 'Order not found', 1;
    IF @Status <> 'Placed'
        THROW 50002, 'Only a Placed order can be confirmed', 1;
    IF NOT EXISTS (SELECT 1 FROM dbo.Payments WHERE OrderId = @OrderId AND Status = 'Paid')
        THROW 50004, 'Order is not paid yet', 1;

    UPDATE dbo.Orders
    SET Status = 'Accepted', UpdatedAt = SYSUTCDATETIME(), UpdatedBy = @ChangedBy
    WHERE Id = @OrderId;

    INSERT INTO dbo.OrderStatusHistory (OrderId, FromStatus, ToStatus, ChangedBy)
    VALUES (@OrderId, @Status, 'Accepted', @ChangedBy);

    COMMIT TRANSACTION;
END
GO

/* ---------------------------------------------------------------------
   4. usp_ChangeStatus (ТРАНЗАКЦІЙНА): кухня й доставка.
      Приймає лише переходи Accepted -> Cooking -> ReadyForDelivery ->
      Delivering -> Delivered. Усе інше (в т.ч. Placed->Accepted та
      Cancelled) відхиляється керованою помилкою 50003.
   --------------------------------------------------------------------- */
CREATE OR ALTER PROCEDURE dbo.usp_ChangeStatus
    @OrderId   BIGINT,
    @NewStatus NVARCHAR(30),
    @ChangedBy NVARCHAR(100) = NULL,
    @Note      NVARCHAR(500) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    BEGIN TRANSACTION;

    DECLARE @Current NVARCHAR(30) =
        (SELECT Status FROM dbo.Orders WITH (UPDLOCK, ROWLOCK) WHERE Id = @OrderId AND IsDeleted = 0);

    IF @Current IS NULL
        THROW 50001, 'Order not found', 1;

    IF NOT EXISTS (SELECT 1
                   FROM (VALUES ('Accepted',         'Cooking'),
                                ('Cooking',          'ReadyForDelivery'),
                                ('ReadyForDelivery', 'Delivering'),
                                ('Delivering',       'Delivered')) AS t (FromStatus, ToStatus)
                   WHERE t.FromStatus = @Current AND t.ToStatus = @NewStatus)
        THROW 50003, 'Invalid status transition (Placed->Accepted: usp_ConfirmOrder; Cancelled: usp_CancelOrder)', 1;

    UPDATE dbo.Orders
    SET Status = @NewStatus, UpdatedAt = SYSUTCDATETIME(), UpdatedBy = @ChangedBy
    WHERE Id = @OrderId;

    INSERT INTO dbo.OrderStatusHistory (OrderId, FromStatus, ToStatus, ChangedBy, Note)
    VALUES (@OrderId, @Current, @NewStatus, @ChangedBy, @Note);

    COMMIT TRANSACTION;
END
GO

/* ---------------------------------------------------------------------
   5. usp_CancelOrder (ТРАНЗАКЦІЙНА): скасування лише з Placed або Accepted
      (тобто до Cooking). Оплачений платіж стає Refunded, неоплачений Cancelled.
   --------------------------------------------------------------------- */
CREATE OR ALTER PROCEDURE dbo.usp_CancelOrder
    @OrderId   BIGINT,
    @Reason    NVARCHAR(500) = NULL,
    @ChangedBy NVARCHAR(100) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    BEGIN TRANSACTION;

    DECLARE @Status NVARCHAR(30) =
        (SELECT Status FROM dbo.Orders WITH (UPDLOCK, ROWLOCK) WHERE Id = @OrderId AND IsDeleted = 0);

    IF @Status IS NULL
        THROW 50001, 'Order not found', 1;
    IF @Status NOT IN ('Placed', 'Accepted')
        THROW 50007, 'Cancellation is allowed only before cooking starts', 1;

    UPDATE dbo.Orders
    SET Status = 'Cancelled', UpdatedAt = SYSUTCDATETIME(), UpdatedBy = @ChangedBy
    WHERE Id = @OrderId;

    UPDATE dbo.Payments
    SET Status    = CASE Status WHEN 'Paid' THEN 'Refunded' ELSE 'Cancelled' END,
        UpdatedAt = SYSUTCDATETIME(),
        UpdatedBy = @ChangedBy
    WHERE OrderId = @OrderId AND Status IN ('Pending', 'Paid');

    INSERT INTO dbo.OrderStatusHistory (OrderId, FromStatus, ToStatus, ChangedBy, Note)
    VALUES (@OrderId, @Status, 'Cancelled', @ChangedBy, @Reason);

    COMMIT TRANSACTION;
END
GO

/* ---------------------------------------------------------------------
   6. usp_GetCustomerOrders: історія замовлень клієнта (фільтр за статусом,
      пагінація). Використовує IX_Orders_CustomerId_OrderDate.
   --------------------------------------------------------------------- */
CREATE OR ALTER PROCEDURE dbo.usp_GetCustomerOrders
    @CustomerId BIGINT,
    @Status     NVARCHAR(30) = NULL,
    @PageNumber INT = 1,
    @PageSize   INT = 20
AS
BEGIN
    SET NOCOUNT ON;

    IF @PageNumber < 1 OR @PageSize NOT BETWEEN 1 AND 100
        THROW 50020, 'Invalid paging parameters', 1;

    SELECT o.Id,
           o.Status,
           o.TotalAmount,
           o.OrderDate,
           d.DeliveryAddress,
           (SELECT COUNT(*) FROM dbo.OrderItems oi WHERE oi.OrderId = o.Id) AS ItemCount
    FROM dbo.Orders o
    JOIN dbo.OrderDetails d ON d.OrderId = o.Id
    WHERE o.CustomerId = @CustomerId
      AND o.IsDeleted = 0
      AND (@Status IS NULL OR o.Status = @Status)
    ORDER BY o.OrderDate DESC, o.Id DESC
    OFFSET (@PageNumber - 1) * @PageSize ROWS FETCH NEXT @PageSize ROWS ONLY;
END
GO

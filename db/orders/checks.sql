/* =====================================================================
   Golden Crust | ORDERS | запити перевірки (CRUD, пошук, пагінація,
   транзакційні сценарії, конкурентність). Нічого не ламає в seed-даних:
   зміни виконуються в транзакціях, які відкочуються.
   ===================================================================== */
USE GoldenCrust_Orders;
GO
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
SET ARITHABORT ON;
SET NOCOUNT ON;
GO

-- 0. Усі FK ведуть лише всередині цієї БД (посилань на інші БД немає)
SELECT fk.name                              AS ForeignKey,
       OBJECT_NAME(fk.parent_object_id)     AS ChildTable,
       OBJECT_NAME(fk.referenced_object_id) AS ParentTable,
       fk.delete_referential_action_desc    AS OnDelete
FROM sys.foreign_keys fk
ORDER BY ChildTable, ForeignKey;
GO

-- 1. CRUD на Customers (в транзакції, яка відкочується)
BEGIN TRANSACTION;
    INSERT INTO dbo.Customers (Email, FullName, Phone) VALUES (N'test.user@example.com', N'Тест Користувач', N'+380000000000');   -- Create
    SELECT Id, Email, FullName FROM dbo.Customers WHERE Email = N'test.user@example.com';                                           -- Read
    UPDATE dbo.Customers SET FullName = N'Тест Змінений', UpdatedAt = SYSUTCDATETIME() WHERE Email = N'test.user@example.com';      -- Update
    UPDATE dbo.Customers SET IsDeleted = 1, UpdatedAt = SYSUTCDATETIME() WHERE Email = N'test.user@example.com';                    -- Soft delete
    SELECT COUNT(*) AS VisibleAfterSoftDelete FROM dbo.Customers WHERE Email = N'test.user@example.com' AND IsDeleted = 0;          -- 0
ROLLBACK TRANSACTION;
GO

-- 2. Пошук і пагінація (використовує IX_Orders_CustomerId_OrderDate)
DECLARE @olena BIGINT = (SELECT Id FROM dbo.Customers WHERE Email = N'olena.koval@example.com');
EXEC dbo.usp_GetCustomerOrders @CustomerId = @olena, @PageNumber = 1, @PageSize = 1;   -- сторінка 1
EXEC dbo.usp_GetCustomerOrders @CustomerId = @olena, @PageNumber = 2, @PageSize = 1;   -- сторінка 2
EXEC dbo.usp_GetCustomerOrders @CustomerId = @olena, @Status = 'Delivered';            -- фільтр за статусом
SELECT Id, Email, FullName FROM dbo.Customers WHERE FullName LIKE N'Олена%';           -- пошук за префіксом
GO

-- 3. Історія статусів замовлення (stepper у клієнті; IX_OrderStatusHistory_OrderId_ChangedAt)
DECLARE @delivered BIGINT = (SELECT TOP (1) Id FROM dbo.Orders WHERE Status = 'Delivered' ORDER BY Id);
SELECT FromStatus, ToStatus, ChangedAt, ChangedBy FROM dbo.OrderStatusHistory WHERE OrderId = @delivered ORDER BY ChangedAt, Id;
GO

-- 4. Знімок ціни: змінюємо ціну в довіднику, а UnitPrice у позиціях лишається старим
BEGIN TRANSACTION;
    UPDATE dbo.MenuItems SET Price = Price + 100, UpdatedAt = SYSUTCDATETIME() WHERE Id = 1;
    SELECT m.Price AS CurrentPrice, oi.UnitPrice AS SnapshotPrice, oi.ProductName
    FROM dbo.OrderItems oi JOIN dbo.MenuItems m ON m.Id = oi.MenuItemId
    WHERE oi.MenuItemId = 1;
ROLLBACK TRANSACTION;
GO

-- 5. Транзакційні сценарії: кожен має завершитися КЕРОВАНОЮ помилкою
DECLARE @unpaid BIGINT    = (SELECT TOP (1) Id FROM dbo.Orders WHERE Status = 'Placed' ORDER BY Id);
DECLARE @delivered2 BIGINT = (SELECT TOP (1) Id FROM dbo.Orders WHERE Status = 'Delivered' ORDER BY Id);
DECLARE @cooking BIGINT   = (SELECT TOP (1) Id FROM dbo.Orders WHERE Status = 'Cooking' ORDER BY Id);

-- 5a. Підтвердити неоплачене замовлення -> 50004
BEGIN TRY
    EXEC dbo.usp_ConfirmOrder @OrderId = @unpaid;
    PRINT '5a UNEXPECTED: unpaid order was confirmed';
END TRY
BEGIN CATCH
    IF XACT_STATE() <> 0 ROLLBACK TRANSACTION;
    PRINT CONCAT('5a OK  -> ', ERROR_NUMBER(), ': ', ERROR_MESSAGE());
END CATCH;

-- 5b. Недопустимий перехід Placed -> Cooking -> 50003
BEGIN TRY
    EXEC dbo.usp_ChangeStatus @OrderId = @unpaid, @NewStatus = 'Cooking';
    PRINT '5b UNEXPECTED: Placed -> Cooking succeeded';
END TRY
BEGIN CATCH
    IF XACT_STATE() <> 0 ROLLBACK TRANSACTION;
    PRINT CONCAT('5b OK  -> ', ERROR_NUMBER(), ': ', ERROR_MESSAGE());
END CATCH;

-- 5c. Скасувати замовлення, яке вже готується -> 50007
BEGIN TRY
    EXEC dbo.usp_CancelOrder @OrderId = @cooking;
    PRINT '5c UNEXPECTED: cooking order was cancelled';
END TRY
BEGIN CATCH
    IF XACT_STATE() <> 0 ROLLBACK TRANSACTION;
    PRINT CONCAT('5c OK  -> ', ERROR_NUMBER(), ': ', ERROR_MESSAGE());
END CATCH;

-- 5d. Повторна оплата вже доставленого замовлення -> 50005
BEGIN TRY
    EXEC dbo.usp_PayOrder @OrderId = @delivered2;
    PRINT '5d UNEXPECTED: delivered order was paid again';
END TRY
BEGIN CATCH
    IF XACT_STATE() <> 0 ROLLBACK TRANSACTION;
    PRINT CONCAT('5d OK  -> ', ERROR_NUMBER(), ': ', ERROR_MESSAGE());
END CATCH;

-- 5e. Дозволений шлях: оплатити й підтвердити (в транзакції, яка відкочується)
BEGIN TRANSACTION;
    EXEC dbo.usp_PayOrder     @OrderId = @unpaid;
    EXEC dbo.usp_ConfirmOrder @OrderId = @unpaid;
    SELECT Id, Status FROM dbo.Orders WHERE Id = @unpaid;   -- Accepted
ROLLBACK TRANSACTION;
GO

-- 6. Оптимістична конкурентність (RowVersion): "друга сесія" встигла змінити рядок раніше
BEGIN TRANSACTION;
    DECLARE @rv BINARY(8) = (SELECT RowVersion FROM dbo.Customers WHERE Email = N'olena.koval@example.com');

    UPDATE dbo.Customers SET Phone = N'+380500000000', UpdatedAt = SYSUTCDATETIME()      -- "інша сесія"
    WHERE Email = N'olena.koval@example.com';

    UPDATE dbo.Customers SET FullName = N'Застаріла правка', UpdatedAt = SYSUTCDATETIME() -- наша застаріла правка
    WHERE Email = N'olena.koval@example.com' AND RowVersion = @rv;

    IF @@ROWCOUNT = 0 PRINT '6 OK  -> concurrency conflict detected (0 rows updated)';
    ELSE              PRINT '6 UNEXPECTED: stale update was applied';
ROLLBACK TRANSACTION;
GO

-- 7. Черга активних замовлень (IX_Orders_Status_OrderDate_Active)
SELECT Id, Status, OrderDate FROM dbo.Orders
WHERE IsDeleted = 0 AND Status <> 'Delivered' AND Status <> 'Cancelled'
ORDER BY Status, OrderDate;
GO

-- 8. Ідемпотентність seed: запустіть seed.sql ще раз і порівняйте лічильники
SELECT (SELECT COUNT(*) FROM dbo.Customers) AS Customers,
       (SELECT COUNT(*) FROM dbo.MenuItems) AS MenuItems,
       (SELECT COUNT(*) FROM dbo.Orders)    AS Orders,
       (SELECT COUNT(*) FROM dbo.Payments)  AS Payments;   -- 3 / 8 / 4 / 4
GO

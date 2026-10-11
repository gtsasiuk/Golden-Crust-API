/* =====================================================================
   Golden Crust | контекст ORDERS | Крок 3/3: тестові дані
   Ідемпотентний: кожен INSERT перевіряє наявність запису, а демо-замовлення
   створюються лише якщо таблиця Orders порожня.
   ===================================================================== */
USE GoldenCrust_Orders;
GO
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
SET ARITHABORT ON;
SET NOCOUNT ON;
GO

-- Клієнти (find-or-create за унікальним Email) --------------------------
INSERT INTO dbo.Customers (Email, FullName, Phone, CreatedBy)
SELECT v.Email, v.FullName, v.Phone, N'seed'
FROM (VALUES
    (N'olena.koval@example.com',   N'Олена Коваль',   N'+380501112233'),
    (N'andriy.melnyk@example.com', N'Андрій Мельник', N'+380672223344'),
    (N'iryna.shevchuk@example.com',N'Ірина Шевчук',   N'+380933334455')
) AS v (Email, FullName, Phone)
WHERE NOT EXISTS (SELECT 1 FROM dbo.Customers c WHERE c.Email = v.Email);
GO

-- Локальна копія довідника Catalog (ті самі Id, що в БД Catalog) --------
-- До лаби 8 заповнюється seed-ом, після неї оновлюється подіями з Catalog.
INSERT INTO dbo.MenuItems (Id, Name, Price, IsAvailable, CreatedBy)
SELECT v.Id, v.Name, v.Price, 1, N'seed'
FROM (VALUES
    (1, N'Маргарита',       149.00),
    (2, N'Пепероні',        179.00),
    (3, N'Чотири сири',     189.00),
    (4, N'Діабола',         185.00),
    (5, N'Цезар з куркою',  139.00),
    (6, N'Грецький салат',  119.00),
    (7, N'Тірамісу',         99.00),
    (8, N'Домашній лимонад', 59.00)
) AS v (Id, Name, Price)
WHERE NOT EXISTS (SELECT 1 FROM dbo.MenuItems m WHERE m.Id = v.Id);
GO

-- Демо-замовлення в різних статусах (через процедури, тож заодно їх перевіряємо)
IF NOT EXISTS (SELECT 1 FROM dbo.Orders)
BEGIN
    DECLARE @olena  BIGINT = (SELECT Id FROM dbo.Customers WHERE Email = N'olena.koval@example.com');
    DECLARE @andriy BIGINT = (SELECT Id FROM dbo.Customers WHERE Email = N'andriy.melnyk@example.com');
    DECLARE @iryna  BIGINT = (SELECT Id FROM dbo.Customers WHERE Email = N'iryna.shevchuk@example.com');
    DECLARE @o1 BIGINT, @o2 BIGINT, @o3 BIGINT, @o4 BIGINT;

    -- 1) Delivered (повний шлях)
    EXEC dbo.usp_CreateOrder @CustomerId = @olena, @DeliveryAddress = N'вул. Університетська, 10, кв. 4',
         @ItemsJson = N'[{"menuItemId":1,"quantity":2},{"menuItemId":8,"quantity":2}]',
         @DeliveryLat = 48.291500, @DeliveryLng = 25.935800, @Comment = N'Без цибулі, будь ласка',
         @CreatedBy = N'seed', @OrderId = @o1 OUTPUT;
    EXEC dbo.usp_PayOrder      @OrderId = @o1, @ChangedBy = N'seed';
    EXEC dbo.usp_ConfirmOrder  @OrderId = @o1, @ChangedBy = N'seed';
    EXEC dbo.usp_ChangeStatus  @OrderId = @o1, @NewStatus = 'Cooking',          @ChangedBy = N'seed';
    EXEC dbo.usp_ChangeStatus  @OrderId = @o1, @NewStatus = 'ReadyForDelivery', @ChangedBy = N'seed';
    EXEC dbo.usp_ChangeStatus  @OrderId = @o1, @NewStatus = 'Delivering',       @ChangedBy = N'seed';
    EXEC dbo.usp_ChangeStatus  @OrderId = @o1, @NewStatus = 'Delivered',        @ChangedBy = N'seed';

    -- 2) Cooking (оплачене, готується)
    EXEC dbo.usp_CreateOrder @CustomerId = @olena, @DeliveryAddress = N'вул. Університетська, 10, кв. 4',
         @ItemsJson = N'[{"menuItemId":2,"quantity":1},{"menuItemId":5,"quantity":1},{"menuItemId":7,"quantity":1}]',
         @DeliveryLat = 48.291500, @DeliveryLng = 25.935800, @CreatedBy = N'seed', @OrderId = @o2 OUTPUT;
    EXEC dbo.usp_PayOrder      @OrderId = @o2, @ChangedBy = N'seed';
    EXEC dbo.usp_ConfirmOrder  @OrderId = @o2, @ChangedBy = N'seed';
    EXEC dbo.usp_ChangeStatus  @OrderId = @o2, @NewStatus = 'Cooking', @ChangedBy = N'seed';

    -- 3) Placed, НЕ оплачене (для демонстрації відмови usp_ConfirmOrder)
    EXEC dbo.usp_CreateOrder @CustomerId = @andriy, @DeliveryAddress = N'вул. Героїв Майдану, 25',
         @ItemsJson = N'[{"menuItemId":3,"quantity":1},{"menuItemId":4,"quantity":1}]',
         @DeliveryLat = 48.290100, @DeliveryLng = 25.940200, @Comment = N'Домофон не працює, дзвоніть',
         @CreatedBy = N'seed', @OrderId = @o3 OUTPUT;

    -- 4) Cancelled
    EXEC dbo.usp_CreateOrder @CustomerId = @iryna, @DeliveryAddress = N'вул. Руська, 50',
         @ItemsJson = N'[{"menuItemId":6,"quantity":2}]', @CreatedBy = N'seed', @OrderId = @o4 OUTPUT;
    EXEC dbo.usp_CancelOrder   @OrderId = @o4, @Reason = N'Передумала', @ChangedBy = N'seed';
END
GO

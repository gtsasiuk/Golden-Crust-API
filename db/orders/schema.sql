/* =====================================================================
   Golden Crust | контекст ORDERS | SQL Server | ADO.NET + Dapper
   Крок 1/3: схема. Скрипт ідемпотентний (можна запускати повторно).
   Запуск через sqlcmd: обов'язково з ключем -I (QUOTED_IDENTIFIER ON),
   інакше таблиці з фільтрованими індексами не прийматимуть INSERT/UPDATE.
   ===================================================================== */
IF DB_ID(N'GoldenCrust_Orders') IS NULL
    CREATE DATABASE GoldenCrust_Orders;
GO
USE GoldenCrust_Orders;
GO
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
SET ARITHABORT ON;
GO

/* ---------------------------------------------------------------------
   PK: BIGINT IDENTITY (у БД пише лише один сервіс, вставка послідовна,
   вузький кластерний ключ). Між БД жодних FOREIGN KEY: посилання на
   Catalog (MenuItemId) - звичайний стовпець.
   Аудит: CreatedAt/CreatedBy/UpdatedAt/UpdatedBy/IsDeleted + RowVersion
   на таблицях, де можливий паралельний запис одного рядка.
   --------------------------------------------------------------------- */

-- 1) Customers (власник: Orders) --------------------------------------
IF OBJECT_ID(N'dbo.Customers', N'U') IS NULL
CREATE TABLE dbo.Customers (
    Id         BIGINT        IDENTITY(1,1) NOT NULL,
    Email      NVARCHAR(256) NOT NULL,
    FullName   NVARCHAR(200) NOT NULL,
    Phone      NVARCHAR(30)  NULL,
    CreatedAt  DATETIME2     NOT NULL DEFAULT SYSUTCDATETIME(),
    CreatedBy  NVARCHAR(100) NULL,
    UpdatedAt  DATETIME2     NULL,
    UpdatedBy  NVARCHAR(100) NULL,
    IsDeleted  BIT           NOT NULL DEFAULT 0,
    RowVersion ROWVERSION    NOT NULL,
    CONSTRAINT PK_Customers PRIMARY KEY (Id),
    CONSTRAINT UQ_Customers_Email UNIQUE (Email),
    CONSTRAINT CK_Customers_Email CHECK (Email LIKE N'%_@_%')
);
GO

-- 2) MenuItems: ЛОКАЛЬНА КОПІЯ довідника Catalog (власник: Catalog) -----
--    Id НЕ IDENTITY: це той самий Id, що в БД Catalog.
IF OBJECT_ID(N'dbo.MenuItems', N'U') IS NULL
CREATE TABLE dbo.MenuItems (
    Id          BIGINT         NOT NULL,
    Name        NVARCHAR(200)  NOT NULL,
    Price       DECIMAL(10,2)  NOT NULL,
    IsAvailable BIT            NOT NULL DEFAULT 1,
    CreatedAt   DATETIME2      NOT NULL DEFAULT SYSUTCDATETIME(),
    CreatedBy   NVARCHAR(100)  NULL,
    UpdatedAt   DATETIME2      NULL,
    UpdatedBy   NVARCHAR(100)  NULL,
    IsDeleted   BIT            NOT NULL DEFAULT 0,
    RowVersion  ROWVERSION     NOT NULL,
    CONSTRAINT PK_MenuItems PRIMARY KEY (Id),
    CONSTRAINT CK_MenuItems_Price CHECK (Price > 0)
);
GO

-- 3) Orders: 1:N Customers -> Orders ----------------------------------
--    Без каскаду від Customers: історія замовлень не має зникати.
IF OBJECT_ID(N'dbo.Orders', N'U') IS NULL
CREATE TABLE dbo.Orders (
    Id          BIGINT         IDENTITY(1,1) NOT NULL,
    CustomerId  BIGINT         NOT NULL,
    Status      NVARCHAR(30)   NOT NULL DEFAULT 'Placed',
    TotalAmount DECIMAL(10,2)  NOT NULL DEFAULT 0,
    OrderDate   DATETIME2      NOT NULL DEFAULT SYSUTCDATETIME(),
    CreatedAt   DATETIME2      NOT NULL DEFAULT SYSUTCDATETIME(),
    CreatedBy   NVARCHAR(100)  NULL,
    UpdatedAt   DATETIME2      NULL,
    UpdatedBy   NVARCHAR(100)  NULL,
    IsDeleted   BIT            NOT NULL DEFAULT 0,
    RowVersion  ROWVERSION     NOT NULL,
    CONSTRAINT PK_Orders PRIMARY KEY (Id),
    CONSTRAINT FK_Orders_Customers FOREIGN KEY (CustomerId) REFERENCES dbo.Customers (Id),
    CONSTRAINT CK_Orders_Status CHECK (Status IN
        ('Placed','Accepted','Cooking','ReadyForDelivery','Delivering','Delivered','Cancelled')),
    CONSTRAINT CK_Orders_TotalAmount CHECK (TotalAmount >= 0)
);
GO

-- 4) OrderDetails: 1:1 Orders <-> OrderDetails (PK = FK) ----------------
IF OBJECT_ID(N'dbo.OrderDetails', N'U') IS NULL
CREATE TABLE dbo.OrderDetails (
    OrderId         BIGINT         NOT NULL,
    DeliveryAddress NVARCHAR(400)  NOT NULL,
    DeliveryLat     DECIMAL(9,6)   NULL,   -- для карти в KMP-клієнті (бонус)
    DeliveryLng     DECIMAL(9,6)   NULL,
    Comment         NVARCHAR(1000) NULL,
    CreatedAt       DATETIME2      NOT NULL DEFAULT SYSUTCDATETIME(),
    CONSTRAINT PK_OrderDetails PRIMARY KEY (OrderId),
    CONSTRAINT FK_OrderDetails_Orders FOREIGN KEY (OrderId) REFERENCES dbo.Orders (Id) ON DELETE CASCADE,
    CONSTRAINT CK_OrderDetails_Geo CHECK (
        (DeliveryLat IS NULL AND DeliveryLng IS NULL)
        OR (DeliveryLat IS NOT NULL AND DeliveryLng IS NOT NULL
            AND DeliveryLat BETWEEN -90 AND 90 AND DeliveryLng BETWEEN -180 AND 180))
);
GO

-- 5) OrderItems: M:N Orders <-> MenuItems (проміжна таблиця) -------------
--    ProductName, UnitPrice = ЗНІМОК на момент замовлення (не оновлюється).
IF OBJECT_ID(N'dbo.OrderItems', N'U') IS NULL
CREATE TABLE dbo.OrderItems (
    OrderId     BIGINT         NOT NULL,
    MenuItemId  BIGINT         NOT NULL,
    ProductName NVARCHAR(200)  NOT NULL,
    UnitPrice   DECIMAL(10,2)  NOT NULL,
    Quantity    INT            NOT NULL,
    LineTotal   AS (UnitPrice * Quantity) PERSISTED,
    CreatedAt   DATETIME2      NOT NULL DEFAULT SYSUTCDATETIME(),
    CONSTRAINT PK_OrderItems PRIMARY KEY (OrderId, MenuItemId),
    CONSTRAINT FK_OrderItems_Orders    FOREIGN KEY (OrderId)    REFERENCES dbo.Orders (Id) ON DELETE CASCADE,
    CONSTRAINT FK_OrderItems_MenuItems FOREIGN KEY (MenuItemId) REFERENCES dbo.MenuItems (Id),
    CONSTRAINT CK_OrderItems_UnitPrice CHECK (UnitPrice > 0),
    CONSTRAINT CK_OrderItems_Quantity  CHECK (Quantity > 0)
);
GO

-- 6) Payments: 1:N Orders -> Payments (імітація оплати, без шлюзу) -------
--    Платежі не видаляємо каскадом: це фінансова історія.
IF OBJECT_ID(N'dbo.Payments', N'U') IS NULL
CREATE TABLE dbo.Payments (
    Id             BIGINT        IDENTITY(1,1) NOT NULL,
    OrderId        BIGINT        NOT NULL,
    Amount         DECIMAL(10,2) NOT NULL,
    Status         NVARCHAR(20)  NOT NULL DEFAULT 'Pending',
    PaidAt         DATETIME2     NULL,
    TransactionRef NVARCHAR(100) NULL,
    CreatedAt      DATETIME2     NOT NULL DEFAULT SYSUTCDATETIME(),
    CreatedBy      NVARCHAR(100) NULL,
    UpdatedAt      DATETIME2     NULL,
    UpdatedBy      NVARCHAR(100) NULL,
    IsDeleted      BIT           NOT NULL DEFAULT 0,
    RowVersion     ROWVERSION    NOT NULL,
    CONSTRAINT PK_Payments PRIMARY KEY (Id),
    CONSTRAINT FK_Payments_Orders FOREIGN KEY (OrderId) REFERENCES dbo.Orders (Id),
    CONSTRAINT CK_Payments_Amount CHECK (Amount > 0),
    CONSTRAINT CK_Payments_Status CHECK (Status IN ('Pending','Paid','Refunded','Cancelled')),
    CONSTRAINT CK_Payments_PaidAt CHECK (Status <> 'Paid' OR PaidAt IS NOT NULL)
);
GO

-- 7) OrderStatusHistory: 1:N Orders -> історія (append-only) -------------
IF OBJECT_ID(N'dbo.OrderStatusHistory', N'U') IS NULL
CREATE TABLE dbo.OrderStatusHistory (
    Id         BIGINT        IDENTITY(1,1) NOT NULL,
    OrderId    BIGINT        NOT NULL,
    FromStatus NVARCHAR(30)  NULL,
    ToStatus   NVARCHAR(30)  NOT NULL,
    ChangedAt  DATETIME2     NOT NULL DEFAULT SYSUTCDATETIME(),
    ChangedBy  NVARCHAR(100) NULL,
    Note       NVARCHAR(500) NULL,
    CONSTRAINT PK_OrderStatusHistory PRIMARY KEY (Id),
    CONSTRAINT FK_OrderStatusHistory_Orders FOREIGN KEY (OrderId) REFERENCES dbo.Orders (Id) ON DELETE CASCADE,
    CONSTRAINT CK_OrderStatusHistory_To CHECK (ToStatus IN
        ('Placed','Accepted','Cooking','ReadyForDelivery','Delivering','Delivered','Cancelled'))
);
GO

/* ---------------------------------------------------------------------
   ІНДЕКСИ (кожен під конкретний запит; "сліпих" індексів немає)
   Неявні індекси: UQ_Customers_Email (вхід/пошук за email), усі PK.
   --------------------------------------------------------------------- */

-- Запит: usp_GetCustomerOrders - "замовлення клієнта, нові першими, з пагінацією".
-- Покриваючий: Status і TotalAmount в INCLUDE, звернення до таблиці не потрібне.
-- Фільтрований: "живі" записи (IsDeleted = 0).
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Orders_CustomerId_OrderDate' AND object_id = OBJECT_ID(N'dbo.Orders'))
    CREATE INDEX IX_Orders_CustomerId_OrderDate
        ON dbo.Orders (CustomerId, OrderDate DESC) INCLUDE (Status, TotalAmount)
        WHERE IsDeleted = 0;
GO

-- Запит: "активні замовлення за статусом, найстаріші першими" (черга обробки / фонова симуляція).
-- Фільтрований: завершені й скасовані замовлення (більшість таблиці з часом) в індекс не потрапляють.
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Orders_Status_OrderDate_Active' AND object_id = OBJECT_ID(N'dbo.Orders'))
    CREATE INDEX IX_Orders_Status_OrderDate_Active
        ON dbo.Orders (Status, OrderDate) INCLUDE (CustomerId)
        WHERE IsDeleted = 0 AND Status <> 'Delivered' AND Status <> 'Cancelled';
GO

-- Запит: "у яких замовленнях є ця страва" (звіти популярності). PK (OrderId, MenuItemId)
-- обслуговує лише пошук від замовлення, а зворотний напрямок - цей індекс.
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_OrderItems_MenuItemId' AND object_id = OBJECT_ID(N'dbo.OrderItems'))
    CREATE INDEX IX_OrderItems_MenuItemId ON dbo.OrderItems (MenuItemId) INCLUDE (Quantity);
GO

-- Запит: stepper статусів у клієнті - "історія статусів замовлення за часом".
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_OrderStatusHistory_OrderId_ChangedAt' AND object_id = OBJECT_ID(N'dbo.OrderStatusHistory'))
    CREATE INDEX IX_OrderStatusHistory_OrderId_ChangedAt ON dbo.OrderStatusHistory (OrderId, ChangedAt);
GO

-- Інваріант: у замовлення не більше ОДНОГО "живого" платежу (Pending або Paid);
-- скасовані/повернені платежі в обмеження не входять.
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'UX_Payments_OrderId_Active' AND object_id = OBJECT_ID(N'dbo.Payments'))
    CREATE UNIQUE INDEX UX_Payments_OrderId_Active
        ON dbo.Payments (OrderId)
        WHERE Status <> 'Cancelled' AND Status <> 'Refunded';
GO

/* =====================================================================
   Golden Crust | контекст CATALOG | SQL Server | EF Core (Code First)
   Це DDL-еталон схеми для лаби 1. У лабі 3 ту саму схему створюватимуть
   EF Core міграції (Fluent API), а цей файл лишається для порівняння.
   Скрипт ідемпотентний. Запуск через sqlcmd: ключ -I обов'язковий.
   ===================================================================== */
IF DB_ID(N'GoldenCrust_Catalog') IS NULL
    CREATE DATABASE GoldenCrust_Catalog;
GO
USE GoldenCrust_Catalog;
GO
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
SET ARITHABORT ON;
GO

-- 1) MenuCategories: 1:N MenuCategories -> MenuItems ---------------------
IF OBJECT_ID(N'dbo.MenuCategories', N'U') IS NULL
CREATE TABLE dbo.MenuCategories (
    Id         BIGINT        IDENTITY(1,1) NOT NULL,
    Name       NVARCHAR(100) NOT NULL,
    SortOrder  INT           NOT NULL DEFAULT 0,
    CreatedAt  DATETIME2     NOT NULL DEFAULT SYSUTCDATETIME(),
    CreatedBy  NVARCHAR(100) NULL,
    UpdatedAt  DATETIME2     NULL,
    UpdatedBy  NVARCHAR(100) NULL,
    IsDeleted  BIT           NOT NULL DEFAULT 0,
    RowVersion ROWVERSION    NOT NULL,
    CONSTRAINT PK_MenuCategories PRIMARY KEY (Id),
    CONSTRAINT UQ_MenuCategories_Name UNIQUE (Name)
);
GO

-- 2) MenuItems (власник даних про страви для всіх інших контекстів) -------
--    FK на категорію БЕЗ каскаду: категорію з стравами видалити не можна.
IF OBJECT_ID(N'dbo.MenuItems', N'U') IS NULL
CREATE TABLE dbo.MenuItems (
    Id          BIGINT         IDENTITY(1,1) NOT NULL,
    CategoryId  BIGINT         NOT NULL,
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
    CONSTRAINT FK_MenuItems_MenuCategories FOREIGN KEY (CategoryId) REFERENCES dbo.MenuCategories (Id),
    CONSTRAINT CK_MenuItems_Price CHECK (Price > 0)
);
GO

-- 3) MenuItemDetails: 1:1 MenuItems <-> MenuItemDetails (PK = FK) --------
IF OBJECT_ID(N'dbo.MenuItemDetails', N'U') IS NULL
CREATE TABLE dbo.MenuItemDetails (
    MenuItemId  BIGINT         NOT NULL,
    Description NVARCHAR(2000) NULL,
    Ingredients NVARCHAR(1000) NULL,
    WeightGrams INT            NULL,
    Calories    INT            NULL,
    CreatedAt   DATETIME2      NOT NULL DEFAULT SYSUTCDATETIME(),
    UpdatedAt   DATETIME2      NULL,
    CONSTRAINT PK_MenuItemDetails PRIMARY KEY (MenuItemId),
    CONSTRAINT FK_MenuItemDetails_MenuItems FOREIGN KEY (MenuItemId) REFERENCES dbo.MenuItems (Id) ON DELETE CASCADE,
    CONSTRAINT CK_MenuItemDetails_Weight   CHECK (WeightGrams IS NULL OR WeightGrams > 0),
    CONSTRAINT CK_MenuItemDetails_Calories CHECK (Calories IS NULL OR Calories >= 0)
);
GO

-- 4) MenuItemImages: 1:N MenuItems -> MenuItemImages ---------------------
IF OBJECT_ID(N'dbo.MenuItemImages', N'U') IS NULL
CREATE TABLE dbo.MenuItemImages (
    Id         BIGINT        IDENTITY(1,1) NOT NULL,
    MenuItemId BIGINT        NOT NULL,
    Url        NVARCHAR(500) NOT NULL,
    AltText    NVARCHAR(200) NULL,
    IsPrimary  BIT           NOT NULL DEFAULT 0,
    SortOrder  INT           NOT NULL DEFAULT 0,
    CreatedAt  DATETIME2     NOT NULL DEFAULT SYSUTCDATETIME(),
    CONSTRAINT PK_MenuItemImages PRIMARY KEY (Id),
    CONSTRAINT FK_MenuItemImages_MenuItems FOREIGN KEY (MenuItemId) REFERENCES dbo.MenuItems (Id) ON DELETE CASCADE
);
GO

-- 5) Tags -------------------------------------------------------------
IF OBJECT_ID(N'dbo.Tags', N'U') IS NULL
CREATE TABLE dbo.Tags (
    Id        BIGINT       IDENTITY(1,1) NOT NULL,
    Name      NVARCHAR(50) NOT NULL,
    CreatedAt DATETIME2    NOT NULL DEFAULT SYSUTCDATETIME(),
    CONSTRAINT PK_Tags PRIMARY KEY (Id),
    CONSTRAINT UQ_Tags_Name UNIQUE (Name)
);
GO

-- 6) MenuItemTags: M:N MenuItems <-> Tags зі ВЛАСНИМ полем AddedAt -------
IF OBJECT_ID(N'dbo.MenuItemTags', N'U') IS NULL
CREATE TABLE dbo.MenuItemTags (
    MenuItemId BIGINT    NOT NULL,
    TagId      BIGINT    NOT NULL,
    AddedAt    DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
    CONSTRAINT PK_MenuItemTags PRIMARY KEY (MenuItemId, TagId),
    CONSTRAINT FK_MenuItemTags_MenuItems FOREIGN KEY (MenuItemId) REFERENCES dbo.MenuItems (Id) ON DELETE CASCADE,
    CONSTRAINT FK_MenuItemTags_Tags      FOREIGN KEY (TagId)      REFERENCES dbo.Tags (Id)      ON DELETE CASCADE
);
GO

/* ---------------------------------------------------------------------
   ІНДЕКСИ (кожен під конкретний запит)
   Неявні: UQ_MenuCategories_Name, UQ_Tags_Name, усі PK.
   --------------------------------------------------------------------- */

-- Запит: "меню за категорією, за назвою; лише доступні страви" (головний екран меню).
-- Покриваючий (Price, IsAvailable в INCLUDE) + фільтрований (лише не видалені).
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_MenuItems_CategoryId_Name' AND object_id = OBJECT_ID(N'dbo.MenuItems'))
    CREATE INDEX IX_MenuItems_CategoryId_Name
        ON dbo.MenuItems (CategoryId, Name) INCLUDE (Price, IsAvailable)
        WHERE IsDeleted = 0;
GO

-- Запит: пошук за префіксом назви (LIKE N'Пе%') + інваріант "назва страви унікальна
-- серед не видалених" (видалену страву можна створити знову під тією ж назвою).
-- Увага: LIKE '%текст%' цей індекс не використає.
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'UX_MenuItems_Name' AND object_id = OBJECT_ID(N'dbo.MenuItems'))
    CREATE UNIQUE INDEX UX_MenuItems_Name ON dbo.MenuItems (Name) WHERE IsDeleted = 0;
GO

-- Запит: "усі зображення страви у потрібному порядку" (картка й деталі страви).
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_MenuItemImages_MenuItemId_SortOrder' AND object_id = OBJECT_ID(N'dbo.MenuItemImages'))
    CREATE INDEX IX_MenuItemImages_MenuItemId_SortOrder
        ON dbo.MenuItemImages (MenuItemId, SortOrder) INCLUDE (Url, IsPrimary);
GO

-- Інваріант: у страви лише ОДНЕ головне фото.
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'UX_MenuItemImages_OnePrimary' AND object_id = OBJECT_ID(N'dbo.MenuItemImages'))
    CREATE UNIQUE INDEX UX_MenuItemImages_OnePrimary ON dbo.MenuItemImages (MenuItemId) WHERE IsPrimary = 1;
GO

-- Запит: "усі страви з тегом X" (фільтр за тегом). PK (MenuItemId, TagId) обслуговує
-- лише напрямок від страви до тегів, а зворотний напрямок - цей індекс.
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_MenuItemTags_TagId_MenuItemId' AND object_id = OBJECT_ID(N'dbo.MenuItemTags'))
    CREATE INDEX IX_MenuItemTags_TagId_MenuItemId ON dbo.MenuItemTags (TagId, MenuItemId);
GO

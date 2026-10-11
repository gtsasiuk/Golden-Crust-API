/* =====================================================================
   Golden Crust | CATALOG | запити перевірки (читання, пошук, пагінація,
   обмеження). Зміни виконуються в транзакціях, які відкочуються.
   ===================================================================== */
USE GoldenCrust_Catalog;
GO
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
SET ARITHABORT ON;
SET NOCOUNT ON;
GO

-- 0. Усі FK лише всередині цієї БД; видно, де є каскад, а де ні
SELECT fk.name                              AS ForeignKey,
       OBJECT_NAME(fk.parent_object_id)     AS ChildTable,
       OBJECT_NAME(fk.referenced_object_id) AS ParentTable,
       fk.delete_referential_action_desc    AS OnDelete
FROM sys.foreign_keys fk
ORDER BY ChildTable, ForeignKey;
GO

-- 1. Меню за категорією з тегами (IX_MenuItems_CategoryId_Name, IX_MenuItemTags)
SELECT c.Name AS Category, m.Id, m.Name, m.Price,
       STRING_AGG(t.Name, N', ') AS Tags
FROM dbo.MenuItems m
JOIN dbo.MenuCategories c ON c.Id = m.CategoryId
LEFT JOIN dbo.MenuItemTags mt ON mt.MenuItemId = m.Id
LEFT JOIN dbo.Tags t ON t.Id = mt.TagId
WHERE m.IsDeleted = 0 AND m.IsAvailable = 1
GROUP BY c.Name, c.SortOrder, m.Id, m.Name, m.Price
ORDER BY c.SortOrder, m.Name;
GO

-- 2. Пошук за префіксом назви (UX_MenuItems_Name) і пагінація
SELECT Id, Name, Price FROM dbo.MenuItems WHERE IsDeleted = 0 AND Name LIKE N'Пе%';

SELECT Id, Name, Price
FROM dbo.MenuItems
WHERE IsDeleted = 0
ORDER BY Name
OFFSET 0 ROWS FETCH NEXT 3 ROWS ONLY;
GO

-- 3. Усі страви з тегом "Гостре" (IX_MenuItemTags_TagId_MenuItemId)
SELECT m.Id, m.Name, mt.AddedAt
FROM dbo.Tags t
JOIN dbo.MenuItemTags mt ON mt.TagId = t.Id
JOIN dbo.MenuItems m ON m.Id = mt.MenuItemId
WHERE t.Name = N'Гостре';
GO

-- 4. Картка страви: 1:1 деталі + 1:N зображення
SELECT m.Name, d.Description, d.WeightGrams, d.Calories, i.Url, i.IsPrimary
FROM dbo.MenuItems m
JOIN dbo.MenuItemDetails d ON d.MenuItemId = m.Id
LEFT JOIN dbo.MenuItemImages i ON i.MenuItemId = m.Id
WHERE m.Id = 1
ORDER BY i.SortOrder;
GO

-- 5. Обмеження мають спрацювати (кожен випадок -> керована помилка)
BEGIN TRY
    BEGIN TRANSACTION;
    INSERT INTO dbo.MenuItems (CategoryId, Name, Price) VALUES (1, N'Тест з нульовою ціною', 0);
    PRINT '5a UNEXPECTED: price 0 accepted';
END TRY
BEGIN CATCH
    IF XACT_STATE() <> 0 ROLLBACK TRANSACTION;
    PRINT CONCAT('5a OK  (CHECK Price > 0) -> ', ERROR_NUMBER(), ': ', LEFT(ERROR_MESSAGE(), 90));
END CATCH;

BEGIN TRY
    BEGIN TRANSACTION;
    INSERT INTO dbo.MenuItems (CategoryId, Name, Price) VALUES (1, N'Маргарита', 100);   -- дубль назви
    PRINT '5b UNEXPECTED: duplicate name accepted';
END TRY
BEGIN CATCH
    IF XACT_STATE() <> 0 ROLLBACK TRANSACTION;
    PRINT CONCAT('5b OK  (UNIQUE Name) -> ', ERROR_NUMBER(), ': ', LEFT(ERROR_MESSAGE(), 90));
END CATCH;

BEGIN TRY
    BEGIN TRANSACTION;
    INSERT INTO dbo.MenuItemImages (MenuItemId, Url, IsPrimary) VALUES (1, N'https://x.example/second-primary.jpg', 1);   -- друге головне фото
    PRINT '5c UNEXPECTED: second primary image accepted';
END TRY
BEGIN CATCH
    IF XACT_STATE() <> 0 ROLLBACK TRANSACTION;
    PRINT CONCAT('5c OK  (one primary image) -> ', ERROR_NUMBER(), ': ', LEFT(ERROR_MESSAGE(), 90));
END CATCH;

BEGIN TRY
    BEGIN TRANSACTION;
    DELETE FROM dbo.MenuCategories WHERE Id = 1;   -- категорія зі стравами: без каскаду
    PRINT '5d UNEXPECTED: category with items was deleted';
END TRY
BEGIN CATCH
    IF XACT_STATE() <> 0 ROLLBACK TRANSACTION;
    PRINT CONCAT('5d OK  (no cascade on category) -> ', ERROR_NUMBER(), ': ', LEFT(ERROR_MESSAGE(), 90));
END CATCH;
GO

-- 6. Каскад там, де він доречний: видалення страви прибирає деталі, фото й теги
BEGIN TRANSACTION;
    DELETE FROM dbo.MenuItems WHERE Id = 8;
    SELECT (SELECT COUNT(*) FROM dbo.MenuItemDetails WHERE MenuItemId = 8) AS Details,
           (SELECT COUNT(*) FROM dbo.MenuItemImages  WHERE MenuItemId = 8) AS Images,
           (SELECT COUNT(*) FROM dbo.MenuItemTags    WHERE MenuItemId = 8) AS Tags;   -- 0 / 0 / 0
ROLLBACK TRANSACTION;
GO

-- 7. Ідемпотентність seed: запустіть seed.sql ще раз і порівняйте лічильники
SELECT (SELECT COUNT(*) FROM dbo.MenuCategories) AS Categories,   -- 4
       (SELECT COUNT(*) FROM dbo.MenuItems)      AS Items,        -- 8
       (SELECT COUNT(*) FROM dbo.MenuItemDetails) AS Details,     -- 8
       (SELECT COUNT(*) FROM dbo.MenuItemImages) AS Images,       -- 10
       (SELECT COUNT(*) FROM dbo.Tags)           AS Tags,         -- 3
       (SELECT COUNT(*) FROM dbo.MenuItemTags)   AS ItemTags;     -- 8
GO

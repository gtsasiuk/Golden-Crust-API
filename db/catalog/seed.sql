/* =====================================================================
   Golden Crust | контекст CATALOG | довідникові дані (seed)
   Ідентифікатори задані явно (IDENTITY_INSERT), бо Orders і Reviews
   посилаються на ті самі Id. Перед вставкою перевіряємо наявність за Id,
   тож повторний запуск не створює дублікатів.
   ===================================================================== */
USE GoldenCrust_Catalog;
GO
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
SET ARITHABORT ON;
SET NOCOUNT ON;
GO

-- Категорії --------------------------------------------------------------
SET IDENTITY_INSERT dbo.MenuCategories ON;
INSERT INTO dbo.MenuCategories (Id, Name, SortOrder, CreatedBy)
SELECT v.Id, v.Name, v.SortOrder, N'seed'
FROM (VALUES (1, N'Піци', 1), (2, N'Салати', 2), (3, N'Десерти', 3), (4, N'Напої', 4)) AS v (Id, Name, SortOrder)
WHERE NOT EXISTS (SELECT 1 FROM dbo.MenuCategories c WHERE c.Id = v.Id);
SET IDENTITY_INSERT dbo.MenuCategories OFF;
GO

-- Страви -----------------------------------------------------------------
SET IDENTITY_INSERT dbo.MenuItems ON;
INSERT INTO dbo.MenuItems (Id, CategoryId, Name, Price, IsAvailable, CreatedBy)
SELECT v.Id, v.CategoryId, v.Name, v.Price, 1, N'seed'
FROM (VALUES
    (1, 1, N'Маргарита',        149.00),
    (2, 1, N'Пепероні',         179.00),
    (3, 1, N'Чотири сири',      189.00),
    (4, 1, N'Діабола',          185.00),
    (5, 2, N'Цезар з куркою',   139.00),
    (6, 2, N'Грецький салат',   119.00),
    (7, 3, N'Тірамісу',          99.00),
    (8, 4, N'Домашній лимонад',  59.00)
) AS v (Id, CategoryId, Name, Price)
WHERE NOT EXISTS (SELECT 1 FROM dbo.MenuItems m WHERE m.Id = v.Id);
SET IDENTITY_INSERT dbo.MenuItems OFF;
GO

-- Деталі страв (1:1) -------------------------------------------------------
INSERT INTO dbo.MenuItemDetails (MenuItemId, Description, Ingredients, WeightGrams, Calories)
SELECT v.MenuItemId, v.Description, v.Ingredients, v.WeightGrams, v.Calories
FROM (VALUES
    (1, N'Класична піца з томатним соусом і моцарелою.',       N'тісто, томати, моцарела, базилік',           450, 980),
    (2, N'Соковита піца з пікантною салямі пепероні.',          N'тісто, томати, моцарела, пепероні',          480, 1250),
    (3, N'Піца з моцарелою, горгонзолою, пармезаном і чеддером.', N'тісто, вершковий соус, чотири сорти сиру',  470, 1320),
    (4, N'Гостра піца з салямі і перцем халапеньйо.',           N'тісто, томати, моцарела, салямі, халапеньйо', 490, 1280),
    (5, N'Салат з курячим філе, пармезаном і грінками.',        N'курка, романо, пармезан, грінки, соус цезар', 280, 420),
    (6, N'Свіжі овочі з сиром фета та оливками.',               N'огірок, томат, фета, оливки, оливкова олія',  250, 260),
    (7, N'Італійський десерт з маскарпоне та кавою.',           N'маскарпоне, савоярді, кава, какао',           150, 390),
    (8, N'Лимонад зі свіжого лимона та м''яти.',                N'лимон, м''ята, цукор, вода',                  400, 140)
) AS v (MenuItemId, Description, Ingredients, WeightGrams, Calories)
WHERE NOT EXISTS (SELECT 1 FROM dbo.MenuItemDetails d WHERE d.MenuItemId = v.MenuItemId);
GO

-- Зображення (1:N): головне фото для кожної страви + друге для двох піц ----
SET IDENTITY_INSERT dbo.MenuItemImages ON;
INSERT INTO dbo.MenuItemImages (Id, MenuItemId, Url, AltText, IsPrimary, SortOrder)
SELECT v.Id, v.MenuItemId, v.Url, v.AltText, v.IsPrimary, v.SortOrder
FROM (VALUES
    ( 1, 1, N'https://cdn.goldencrust.example/img/margherita-1.jpg', N'Піца Маргарита',          1, 0),
    ( 2, 2, N'https://cdn.goldencrust.example/img/pepperoni-1.jpg',  N'Піца Пепероні',           1, 0),
    ( 3, 3, N'https://cdn.goldencrust.example/img/four-cheese-1.jpg',N'Піца Чотири сири',        1, 0),
    ( 4, 4, N'https://cdn.goldencrust.example/img/diavola-1.jpg',    N'Піца Діабола',            1, 0),
    ( 5, 5, N'https://cdn.goldencrust.example/img/caesar-1.jpg',     N'Салат Цезар з куркою',    1, 0),
    ( 6, 6, N'https://cdn.goldencrust.example/img/greek-1.jpg',      N'Грецький салат',          1, 0),
    ( 7, 7, N'https://cdn.goldencrust.example/img/tiramisu-1.jpg',   N'Тірамісу',                1, 0),
    ( 8, 8, N'https://cdn.goldencrust.example/img/lemonade-1.jpg',   N'Домашній лимонад',        1, 0),
    ( 9, 1, N'https://cdn.goldencrust.example/img/margherita-2.jpg', N'Маргарита, вид зверху',   0, 1),
    (10, 2, N'https://cdn.goldencrust.example/img/pepperoni-2.jpg',  N'Пепероні, зріз',          0, 1)
) AS v (Id, MenuItemId, Url, AltText, IsPrimary, SortOrder)
WHERE NOT EXISTS (SELECT 1 FROM dbo.MenuItemImages i WHERE i.Id = v.Id);
SET IDENTITY_INSERT dbo.MenuItemImages OFF;
GO

-- Теги ---------------------------------------------------------------------
SET IDENTITY_INSERT dbo.Tags ON;
INSERT INTO dbo.Tags (Id, Name)
SELECT v.Id, v.Name
FROM (VALUES (1, N'Вегетаріанське'), (2, N'Хіт'), (3, N'Гостре')) AS v (Id, Name)
WHERE NOT EXISTS (SELECT 1 FROM dbo.Tags t WHERE t.Id = v.Id);
SET IDENTITY_INSERT dbo.Tags OFF;
GO

-- Зв'язки страва-тег (M:N; AddedAt заповнюється за замовчуванням) ---------
INSERT INTO dbo.MenuItemTags (MenuItemId, TagId)
SELECT v.MenuItemId, v.TagId
FROM (VALUES (1,1), (3,1), (6,1), (7,1), (8,1), (1,2), (2,2), (4,3)) AS v (MenuItemId, TagId)
WHERE NOT EXISTS (SELECT 1 FROM dbo.MenuItemTags x WHERE x.MenuItemId = v.MenuItemId AND x.TagId = v.TagId);
GO

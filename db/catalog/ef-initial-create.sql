IF OBJECT_ID(N'[__EFMigrationsHistory]') IS NULL
BEGIN
    CREATE TABLE [__EFMigrationsHistory] (
        [MigrationId] nvarchar(150) NOT NULL,
        [ProductVersion] nvarchar(32) NOT NULL,
        CONSTRAINT [PK___EFMigrationsHistory] PRIMARY KEY ([MigrationId])
    );
END;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261024231926_InitialCreate'
)
BEGIN
    CREATE TABLE [MenuCategories] (
        [Id] bigint NOT NULL IDENTITY,
        [Name] nvarchar(100) NOT NULL,
        [SortOrder] int NOT NULL DEFAULT 0,
        [CreatedAt] datetime2 NOT NULL DEFAULT (SYSUTCDATETIME()),
        [CreatedBy] nvarchar(100) NULL,
        [UpdatedAt] datetime2 NULL,
        [UpdatedBy] nvarchar(100) NULL,
        [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit),
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_MenuCategories] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261024231926_InitialCreate'
)
BEGIN
    CREATE TABLE [Tags] (
        [Id] bigint NOT NULL IDENTITY,
        [Name] nvarchar(50) NOT NULL,
        [CreatedAt] datetime2 NOT NULL DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT [PK_Tags] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261024231926_InitialCreate'
)
BEGIN
    CREATE TABLE [MenuItems] (
        [Id] bigint NOT NULL IDENTITY,
        [CategoryId] bigint NOT NULL,
        [Name] nvarchar(200) NOT NULL,
        [Price] decimal(10,2) NOT NULL,
        [IsAvailable] bit NOT NULL,
        [CreatedAt] datetime2 NOT NULL DEFAULT (SYSUTCDATETIME()),
        [CreatedBy] nvarchar(100) NULL,
        [UpdatedAt] datetime2 NULL,
        [UpdatedBy] nvarchar(100) NULL,
        [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit),
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_MenuItems] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_MenuItems_Price] CHECK ([Price] > 0),
        CONSTRAINT [FK_MenuItems_MenuCategories_CategoryId] FOREIGN KEY ([CategoryId]) REFERENCES [MenuCategories] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261024231926_InitialCreate'
)
BEGIN
    CREATE TABLE [MenuItemDetails] (
        [MenuItemId] bigint NOT NULL,
        [Description] nvarchar(2000) NULL,
        [Ingredients] nvarchar(1000) NULL,
        [WeightGrams] int NULL,
        [Calories] int NULL,
        [CreatedAt] datetime2 NOT NULL DEFAULT (SYSUTCDATETIME()),
        [UpdatedAt] datetime2 NULL,
        CONSTRAINT [PK_MenuItemDetails] PRIMARY KEY ([MenuItemId]),
        CONSTRAINT [CK_MenuItemDetails_Calories] CHECK ([Calories] IS NULL OR [Calories] >= 0),
        CONSTRAINT [CK_MenuItemDetails_Weight] CHECK ([WeightGrams] IS NULL OR [WeightGrams] > 0),
        CONSTRAINT [FK_MenuItemDetails_MenuItems_MenuItemId] FOREIGN KEY ([MenuItemId]) REFERENCES [MenuItems] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261024231926_InitialCreate'
)
BEGIN
    CREATE TABLE [MenuItemImages] (
        [Id] bigint NOT NULL IDENTITY,
        [MenuItemId] bigint NOT NULL,
        [Url] nvarchar(500) NOT NULL,
        [AltText] nvarchar(200) NULL,
        [IsPrimary] bit NOT NULL DEFAULT CAST(0 AS bit),
        [SortOrder] int NOT NULL DEFAULT 0,
        [CreatedAt] datetime2 NOT NULL DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT [PK_MenuItemImages] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_MenuItemImages_MenuItems_MenuItemId] FOREIGN KEY ([MenuItemId]) REFERENCES [MenuItems] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261024231926_InitialCreate'
)
BEGIN
    CREATE TABLE [MenuItemTags] (
        [MenuItemId] bigint NOT NULL,
        [TagId] bigint NOT NULL,
        [AddedAt] datetime2 NOT NULL DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT [PK_MenuItemTags] PRIMARY KEY ([MenuItemId], [TagId]),
        CONSTRAINT [FK_MenuItemTags_MenuItems_MenuItemId] FOREIGN KEY ([MenuItemId]) REFERENCES [MenuItems] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_MenuItemTags_Tags_TagId] FOREIGN KEY ([TagId]) REFERENCES [Tags] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261024231926_InitialCreate'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'CreatedAt', N'CreatedBy', N'Name', N'SortOrder', N'UpdatedAt', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[MenuCategories]'))
        SET IDENTITY_INSERT [MenuCategories] ON;
    EXEC(N'INSERT INTO [MenuCategories] ([Id], [CreatedAt], [CreatedBy], [Name], [SortOrder], [UpdatedAt], [UpdatedBy])
    VALUES (CAST(1 AS bigint), ''2026-10-01T00:00:00.0000000Z'', N''seed'', N''Піци'', 1, NULL, NULL),
    (CAST(2 AS bigint), ''2026-10-01T00:00:00.0000000Z'', N''seed'', N''Салати'', 2, NULL, NULL),
    (CAST(3 AS bigint), ''2026-10-01T00:00:00.0000000Z'', N''seed'', N''Десерти'', 3, NULL, NULL),
    (CAST(4 AS bigint), ''2026-10-01T00:00:00.0000000Z'', N''seed'', N''Напої'', 4, NULL, NULL)');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'CreatedAt', N'CreatedBy', N'Name', N'SortOrder', N'UpdatedAt', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[MenuCategories]'))
        SET IDENTITY_INSERT [MenuCategories] OFF;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261024231926_InitialCreate'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'CreatedAt', N'Name') AND [object_id] = OBJECT_ID(N'[Tags]'))
        SET IDENTITY_INSERT [Tags] ON;
    EXEC(N'INSERT INTO [Tags] ([Id], [CreatedAt], [Name])
    VALUES (CAST(1 AS bigint), ''2026-10-01T00:00:00.0000000Z'', N''Вегетаріанське''),
    (CAST(2 AS bigint), ''2026-10-01T00:00:00.0000000Z'', N''Хіт''),
    (CAST(3 AS bigint), ''2026-10-01T00:00:00.0000000Z'', N''Гостре'')');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'CreatedAt', N'Name') AND [object_id] = OBJECT_ID(N'[Tags]'))
        SET IDENTITY_INSERT [Tags] OFF;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261024231926_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [UQ_MenuCategories_Name] ON [MenuCategories] ([Name]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261024231926_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_MenuItemImages_MenuItemId_SortOrder] ON [MenuItemImages] ([MenuItemId], [SortOrder]) INCLUDE ([Url], [IsPrimary]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261024231926_InitialCreate'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [UX_MenuItemImages_OnePrimary] ON [MenuItemImages] ([MenuItemId]) WHERE [IsPrimary] = 1');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261024231926_InitialCreate'
)
BEGIN
    EXEC(N'CREATE INDEX [IX_MenuItems_CategoryId_Name] ON [MenuItems] ([CategoryId], [Name]) INCLUDE ([Price], [IsAvailable]) WHERE [IsDeleted] = 0');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261024231926_InitialCreate'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [UX_MenuItems_Name] ON [MenuItems] ([Name]) WHERE [IsDeleted] = 0');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261024231926_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_MenuItemTags_TagId_MenuItemId] ON [MenuItemTags] ([TagId], [MenuItemId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261024231926_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [UQ_Tags_Name] ON [Tags] ([Name]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261024231926_InitialCreate'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261024231926_InitialCreate', N'10.0.13');
END;

COMMIT;
GO


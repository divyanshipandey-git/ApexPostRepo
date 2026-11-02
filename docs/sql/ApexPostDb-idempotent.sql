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
    WHERE [MigrationId] = N'20261031173410_InitialCreate'
)
BEGIN
    CREATE TABLE [AuditEntries] (
        [Id] bigint NOT NULL IDENTITY,
        [Timestamp] datetime2(7) NOT NULL,
        [StaffId] varchar(128) NULL,
        [Role] varchar(64) NULL,
        [BranchCode] varchar(10) NULL,
        [Controller] nvarchar(128) NOT NULL,
        [Action] nvarchar(128) NOT NULL,
        [RouteValuesJson] nvarchar(max) NOT NULL,
        [StatusCode] int NOT NULL,
        [DurationMilliseconds] bigint NOT NULL,
        [CorrelationId] varchar(128) NULL,
        [CreatedAtUtc] datetime2(7) NOT NULL DEFAULT (SYSUTCDATETIME()),
        [CreatedBy] varchar(128) NULL,
        CONSTRAINT [PK_AuditEntries] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_AuditEntries_Duration] CHECK ([DurationMilliseconds] >= 0),
        CONSTRAINT [CK_AuditEntries_StatusCode] CHECK ([StatusCode] >= 0)
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031173410_InitialCreate'
)
BEGIN
    CREATE TABLE [Branches] (
        [BranchCode] varchar(10) NOT NULL,
        [Name] nvarchar(200) NOT NULL,
        [Region] nvarchar(100) NOT NULL,
        [OpeningTime] time(0) NOT NULL,
        [ClosingTime] time(0) NOT NULL,
        [IsActive] bit NOT NULL DEFAULT CAST(1 AS bit),
        [CreatedAtUtc] datetime2(7) NOT NULL DEFAULT (SYSUTCDATETIME()),
        [CreatedBy] varchar(128) NULL,
        [ModifiedAtUtc] datetime2(7) NULL,
        CONSTRAINT [PK_Branches] PRIMARY KEY ([BranchCode]),
        CONSTRAINT [CK_Branches_OpeningHours] CHECK ([ClosingTime] > [OpeningTime])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031173410_InitialCreate'
)
BEGIN
    CREATE TABLE [JsonImportRuns] (
        [Id] bigint NOT NULL IDENTITY,
        [SourceFingerprint] varchar(64) NOT NULL,
        [ImportedAtUtc] datetime2(7) NOT NULL,
        [ImporterVersion] varchar(32) NOT NULL,
        [SourceCount] int NOT NULL,
        [ImportedCount] int NOT NULL,
        [SkippedCount] int NOT NULL,
        [CreatedAtUtc] datetime2(7) NOT NULL DEFAULT (SYSUTCDATETIME()),
        [CreatedBy] varchar(128) NULL,
        CONSTRAINT [PK_JsonImportRuns] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_JsonImportRuns_Counts] CHECK ([SourceCount] >= 0 AND [ImportedCount] >= 0 AND [SkippedCount] >= 0)
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031173410_InitialCreate'
)
BEGIN
    CREATE TABLE [ProductCategories] (
        [CategoryCode] varchar(32) NOT NULL,
        [Name] nvarchar(100) NOT NULL,
        [CreatedAtUtc] datetime2(7) NOT NULL DEFAULT (SYSUTCDATETIME()),
        [CreatedBy] varchar(128) NULL,
        CONSTRAINT [PK_ProductCategories] PRIMARY KEY ([CategoryCode]),
        CONSTRAINT [CK_ProductCategories_Code] CHECK ([CategoryCode] IN ('Packaging','Stationery','OfficeSupplies'))
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031173410_InitialCreate'
)
BEGIN
    CREATE TABLE [Roles] (
        [RoleCode] varchar(32) NOT NULL,
        [Name] nvarchar(100) NOT NULL,
        [CreatedAtUtc] datetime2(7) NOT NULL DEFAULT (SYSUTCDATETIME()),
        [CreatedBy] varchar(128) NULL,
        CONSTRAINT [PK_Roles] PRIMARY KEY ([RoleCode]),
        CONSTRAINT [CK_Roles_Code] CHECK ([RoleCode] IN ('CounterClerk','BranchManager','Auditor','Administrator'))
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031173410_InitialCreate'
)
BEGIN
    CREATE TABLE [ShippingZones] (
        [Code] varchar(32) NOT NULL,
        [Name] nvarchar(100) NOT NULL,
        [CreatedAtUtc] datetime2(7) NOT NULL DEFAULT (SYSUTCDATETIME()),
        [CreatedBy] varchar(128) NULL,
        [ModifiedAtUtc] datetime2(7) NULL,
        CONSTRAINT [PK_ShippingZones] PRIMARY KEY ([Code])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031173410_InitialCreate'
)
BEGIN
    CREATE TABLE [Counters] (
        [Id] varchar(128) NOT NULL,
        [BranchCode] varchar(10) NOT NULL,
        [Name] nvarchar(200) NOT NULL,
        [IsActive] bit NOT NULL DEFAULT CAST(1 AS bit),
        [CreatedAtUtc] datetime2(7) NOT NULL DEFAULT (SYSUTCDATETIME()),
        [CreatedBy] varchar(128) NULL,
        [ModifiedAtUtc] datetime2(7) NULL,
        CONSTRAINT [PK_Counters] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Counters_Branches] FOREIGN KEY ([BranchCode]) REFERENCES [Branches] ([BranchCode])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031173410_InitialCreate'
)
BEGIN
    CREATE TABLE [Products] (
        [Sku] varchar(20) NOT NULL,
        [Name] nvarchar(200) NOT NULL,
        [Category] varchar(32) NOT NULL,
        [UnitPrice] decimal(18,2) NOT NULL,
        [TaxCategory] varchar(32) NOT NULL,
        [CreatedAtUtc] datetime2(7) NOT NULL DEFAULT (SYSUTCDATETIME()),
        [CreatedBy] varchar(128) NULL,
        [ModifiedAtUtc] datetime2(7) NULL,
        CONSTRAINT [PK_Products] PRIMARY KEY ([Sku]),
        CONSTRAINT [CK_Products_TaxCategory] CHECK ([TaxCategory] IN ('Standard','Reduced','ZeroRated','Exempt')),
        CONSTRAINT [CK_Products_UnitPrice] CHECK ([UnitPrice] >= 0),
        CONSTRAINT [FK_Products_ProductCategories] FOREIGN KEY ([Category]) REFERENCES [ProductCategories] ([CategoryCode])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031173410_InitialCreate'
)
BEGIN
    CREATE TABLE [Staff] (
        [Id] varchar(128) NOT NULL,
        [Name] nvarchar(200) NOT NULL,
        [Email] nvarchar(320) NOT NULL,
        [Role] varchar(32) NOT NULL,
        [HomeBranchCode] varchar(10) NOT NULL,
        [IsActive] bit NOT NULL DEFAULT CAST(1 AS bit),
        [CreatedAtUtc] datetime2(7) NOT NULL DEFAULT (SYSUTCDATETIME()),
        [CreatedBy] varchar(128) NULL,
        [ModifiedAtUtc] datetime2(7) NULL,
        CONSTRAINT [PK_Staff] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Staff_Branches] FOREIGN KEY ([HomeBranchCode]) REFERENCES [Branches] ([BranchCode]),
        CONSTRAINT [FK_Staff_Roles] FOREIGN KEY ([Role]) REFERENCES [Roles] ([RoleCode])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031173410_InitialCreate'
)
BEGIN
    CREATE TABLE [ShippingRates] (
        [ZoneCode] varchar(32) NOT NULL,
        [ServiceLevel] varchar(32) NOT NULL,
        [BasePrice] decimal(18,2) NOT NULL,
        [PricePerKilogram] decimal(18,2) NOT NULL,
        [CreatedAtUtc] datetime2(7) NOT NULL DEFAULT (SYSUTCDATETIME()),
        [CreatedBy] varchar(128) NULL,
        [ModifiedAtUtc] datetime2(7) NULL,
        CONSTRAINT [PK_ShippingRates] PRIMARY KEY ([ZoneCode], [ServiceLevel]),
        CONSTRAINT [CK_ShippingRates_Prices] CHECK ([BasePrice] >= 0 AND [PricePerKilogram] >= 0),
        CONSTRAINT [CK_ShippingRates_ServiceLevel] CHECK ([ServiceLevel] IN ('Standard','Express','Registered')),
        CONSTRAINT [FK_ShippingRates_ShippingZones] FOREIGN KEY ([ZoneCode]) REFERENCES [ShippingZones] ([Code])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031173410_InitialCreate'
)
BEGIN
    CREATE TABLE [BranchStock] (
        [BranchCode] varchar(10) NOT NULL,
        [Sku] varchar(20) NOT NULL,
        [Quantity] int NOT NULL,
        [CreatedAtUtc] datetime2(7) NOT NULL DEFAULT (SYSUTCDATETIME()),
        [CreatedBy] varchar(128) NULL,
        [ModifiedAtUtc] datetime2(7) NULL,
        [RowVersion] rowversion NULL,
        CONSTRAINT [PK_BranchStock] PRIMARY KEY ([BranchCode], [Sku]),
        CONSTRAINT [CK_BranchStock_Quantity_NonNegative] CHECK ([Quantity] >= 0),
        CONSTRAINT [FK_BranchStock_Branches] FOREIGN KEY ([BranchCode]) REFERENCES [Branches] ([BranchCode]),
        CONSTRAINT [FK_BranchStock_Products] FOREIGN KEY ([Sku]) REFERENCES [Products] ([Sku])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031173410_InitialCreate'
)
BEGIN
    CREATE TABLE [CounterTransactions] (
        [Id] varchar(128) NOT NULL,
        [BranchCode] varchar(10) NOT NULL,
        [CounterId] varchar(128) NOT NULL,
        [ClerkId] varchar(128) NOT NULL,
        [Status] varchar(32) NOT NULL,
        [Reason] nvarchar(500) NULL,
        [OccurredAtUtc] datetime2(7) NULL,
        [Subtotal] decimal(18,2) NOT NULL,
        [Tax] decimal(18,2) NOT NULL,
        [Total] decimal(18,2) NOT NULL,
        [RowVersion] rowversion NOT NULL,
        [CreatedAtUtc] datetime2(7) NOT NULL DEFAULT (SYSUTCDATETIME()),
        [CreatedBy] varchar(128) NULL,
        [ModifiedAtUtc] datetime2(7) NULL,
        CONSTRAINT [PK_CounterTransactions] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_CounterTransactions_Status] CHECK ([Status] IN ('Open','Paid','Voided','Refunded')),
        CONSTRAINT [CK_CounterTransactions_Totals] CHECK ([Subtotal] >= 0 AND [Tax] >= 0 AND [Total] >= 0),
        CONSTRAINT [FK_CounterTransactions_Branches] FOREIGN KEY ([BranchCode]) REFERENCES [Branches] ([BranchCode]),
        CONSTRAINT [FK_CounterTransactions_Counters] FOREIGN KEY ([CounterId]) REFERENCES [Counters] ([Id]),
        CONSTRAINT [FK_CounterTransactions_Staff] FOREIGN KEY ([ClerkId]) REFERENCES [Staff] ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031173410_InitialCreate'
)
BEGIN
    CREATE TABLE [StaffCredentials] (
        [StaffId] varchar(128) NOT NULL,
        [PasswordHash] nvarchar(512) NOT NULL,
        [CreatedAtUtc] datetime2(7) NOT NULL DEFAULT (SYSUTCDATETIME()),
        [CreatedBy] varchar(128) NULL,
        [ModifiedAtUtc] datetime2(7) NULL,
        CONSTRAINT [PK_StaffCredentials] PRIMARY KEY ([StaffId]),
        CONSTRAINT [CK_StaffCredentials_Hash] CHECK (LEN([PasswordHash]) > 0),
        CONSTRAINT [FK_StaffCredentials_Staff] FOREIGN KEY ([StaffId]) REFERENCES [Staff] ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031173410_InitialCreate'
)
BEGIN
    CREATE TABLE [MoneyOrders] (
        [Id] varchar(128) NOT NULL,
        [Amount] decimal(18,2) NOT NULL,
        [SenderName] nvarchar(200) NOT NULL,
        [RecipientName] nvarchar(200) NOT NULL,
        [Fee] decimal(18,2) NOT NULL,
        [BranchCode] varchar(10) NULL,
        [TransactionId] varchar(128) NULL,
        [CreatedAtUtc] datetime2(7) NULL,
        [CreatedBy] varchar(128) NULL,
        CONSTRAINT [PK_MoneyOrders] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_MoneyOrders_Amount] CHECK ([Amount] > 0),
        CONSTRAINT [CK_MoneyOrders_Fee] CHECK ([Fee] >= 0),
        CONSTRAINT [FK_MoneyOrders_Branches] FOREIGN KEY ([BranchCode]) REFERENCES [Branches] ([BranchCode]),
        CONSTRAINT [FK_MoneyOrders_CounterTransactions] FOREIGN KEY ([TransactionId]) REFERENCES [CounterTransactions] ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031173410_InitialCreate'
)
BEGIN
    CREATE TABLE [Payments] (
        [TransactionId] varchar(128) NOT NULL,
        [Method] varchar(16) NOT NULL,
        [Amount] decimal(18,2) NOT NULL,
        [PaidAtUtc] datetime2(7) NULL,
        [CreatedAtUtc] datetime2(7) NOT NULL DEFAULT (SYSUTCDATETIME()),
        [CreatedBy] varchar(128) NULL,
        CONSTRAINT [PK_Payments] PRIMARY KEY ([TransactionId]),
        CONSTRAINT [CK_Payments_Amount] CHECK ([Amount] >= 0),
        CONSTRAINT [CK_Payments_Method] CHECK ([Method] IN ('Cash','Card','Upi')),
        CONSTRAINT [FK_Payments_CounterTransactions] FOREIGN KEY ([TransactionId]) REFERENCES [CounterTransactions] ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031173410_InitialCreate'
)
BEGIN
    CREATE TABLE [Shipments] (
        [Id] varchar(128) NOT NULL,
        [ItemType] varchar(16) NOT NULL,
        [WeightKg] decimal(9,3) NOT NULL,
        [LengthCm] decimal(9,2) NOT NULL,
        [WidthCm] decimal(9,2) NOT NULL,
        [HeightCm] decimal(9,2) NOT NULL,
        [ZoneCode] varchar(32) NOT NULL,
        [ServiceLevel] varchar(32) NOT NULL,
        [BranchCode] varchar(10) NULL,
        [CounterTransactionId] varchar(128) NULL,
        [CourierStatus] nvarchar(100) NULL,
        [CourierStatusUpdatedAtUtc] datetime2(7) NULL,
        [CreatedAtUtc] datetime2(7) NULL,
        [CreatedBy] varchar(128) NULL,
        [ModifiedAtUtc] datetime2(7) NULL,
        CONSTRAINT [PK_Shipments] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_Shipments_Dimensions] CHECK ([LengthCm] > 0 AND [WidthCm] > 0 AND [HeightCm] > 0),
        CONSTRAINT [CK_Shipments_ItemType] CHECK ([ItemType] IN ('Parcel','Letter')),
        CONSTRAINT [CK_Shipments_ServiceLevel] CHECK ([ServiceLevel] IN ('Standard','Express','Registered')),
        CONSTRAINT [CK_Shipments_Weight] CHECK ([WeightKg] >= 0),
        CONSTRAINT [FK_Shipments_Branches] FOREIGN KEY ([BranchCode]) REFERENCES [Branches] ([BranchCode]),
        CONSTRAINT [FK_Shipments_CounterTransactions] FOREIGN KEY ([CounterTransactionId]) REFERENCES [CounterTransactions] ([Id]),
        CONSTRAINT [FK_Shipments_ShippingZones] FOREIGN KEY ([ZoneCode]) REFERENCES [ShippingZones] ([Code])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031173410_InitialCreate'
)
BEGIN
    CREATE TABLE [StockMovements] (
        [Id] bigint NOT NULL IDENTITY,
        [BranchCode] varchar(10) NOT NULL,
        [Sku] varchar(20) NOT NULL,
        [QuantityDelta] int NOT NULL,
        [QuantityBefore] int NOT NULL,
        [QuantityAfter] int NOT NULL,
        [MovementType] varchar(24) NOT NULL,
        [TransactionId] varchar(128) NULL,
        [Reason] nvarchar(500) NULL,
        [ChangedBy] varchar(128) NULL,
        [CreatedAtUtc] datetime2(7) NOT NULL DEFAULT (SYSUTCDATETIME()),
        [CreatedBy] varchar(128) NULL,
        CONSTRAINT [PK_StockMovements] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_StockMovements_Delta] CHECK ([QuantityDelta] <> 0),
        CONSTRAINT [CK_StockMovements_Quantities] CHECK ([QuantityBefore] >= 0 AND [QuantityAfter] >= 0),
        CONSTRAINT [FK_StockMovements_Branches] FOREIGN KEY ([BranchCode]) REFERENCES [Branches] ([BranchCode]),
        CONSTRAINT [FK_StockMovements_CounterTransactions] FOREIGN KEY ([TransactionId]) REFERENCES [CounterTransactions] ([Id]),
        CONSTRAINT [FK_StockMovements_Products] FOREIGN KEY ([Sku]) REFERENCES [Products] ([Sku])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031173410_InitialCreate'
)
BEGIN
    CREATE TABLE [TransactionLines] (
        [Id] bigint NOT NULL IDENTITY,
        [TransactionId] varchar(128) NOT NULL,
        [LineNumber] int NOT NULL,
        [Description] nvarchar(500) NOT NULL,
        [ItemType] varchar(16) NOT NULL,
        [Quantity] int NOT NULL,
        [UnitPrice] decimal(18,2) NOT NULL,
        [TaxCategory] varchar(32) NOT NULL,
        [ReferenceId] varchar(128) NULL,
        [LineTotal] decimal(18,2) NOT NULL,
        [CreatedAtUtc] datetime2(7) NOT NULL DEFAULT (SYSUTCDATETIME()),
        [CreatedBy] varchar(128) NULL,
        CONSTRAINT [PK_TransactionLines] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_TransactionLines_Amounts] CHECK ([UnitPrice] >= 0 AND [LineTotal] >= 0),
        CONSTRAINT [CK_TransactionLines_ItemType] CHECK ([ItemType] IN ('Retail','Service')),
        CONSTRAINT [CK_TransactionLines_Quantity] CHECK ([Quantity] > 0),
        CONSTRAINT [CK_TransactionLines_TaxCategory] CHECK ([TaxCategory] IN ('Standard','Reduced','ZeroRated','Exempt')),
        CONSTRAINT [FK_TransactionLines_CounterTransactions] FOREIGN KEY ([TransactionId]) REFERENCES [CounterTransactions] ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031173410_InitialCreate'
)
BEGIN
    CREATE TABLE [TransactionTaxRates] (
        [TransactionId] varchar(128) NOT NULL,
        [Category] varchar(32) NOT NULL,
        [Rate] decimal(9,6) NOT NULL,
        [CreatedAtUtc] datetime2(7) NOT NULL DEFAULT (SYSUTCDATETIME()),
        [CreatedBy] varchar(128) NULL,
        CONSTRAINT [PK_TransactionTaxRates] PRIMARY KEY ([TransactionId], [Category]),
        CONSTRAINT [CK_TransactionTaxRates_Rate] CHECK ([Rate] >= 0 AND [Rate] <= 1),
        CONSTRAINT [FK_TransactionTaxRates_CounterTransactions] FOREIGN KEY ([TransactionId]) REFERENCES [CounterTransactions] ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031173410_InitialCreate'
)
BEGIN
    CREATE TABLE [IdentityVerifications] (
        [Id] varchar(128) NOT NULL,
        [MoneyOrderId] varchar(128) NOT NULL,
        [DocumentType] nvarchar(100) NOT NULL,
        [MaskedDocumentNumber] varchar(8) NOT NULL,
        [IsVerified] bit NOT NULL,
        [VerifiedAtUtc] datetime2(7) NOT NULL,
        [CreatedAtUtc] datetime2(7) NOT NULL DEFAULT (SYSUTCDATETIME()),
        [CreatedBy] varchar(128) NULL,
        CONSTRAINT [PK_IdentityVerifications] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_IdentityVerifications_MaskedNumber] CHECK (LEN([MaskedDocumentNumber]) = 8 AND LEFT([MaskedDocumentNumber], 4) = 'XXXX' AND SUBSTRING([MaskedDocumentNumber], 5, 4) NOT LIKE '%[^0-9A-Za-z]%'),
        CONSTRAINT [FK_IdentityVerifications_MoneyOrders] FOREIGN KEY ([MoneyOrderId]) REFERENCES [MoneyOrders] ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031173410_InitialCreate'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'CategoryCode', N'CreatedBy', N'Name') AND [object_id] = OBJECT_ID(N'[ProductCategories]'))
        SET IDENTITY_INSERT [ProductCategories] ON;
    EXEC(N'INSERT INTO [ProductCategories] ([CategoryCode], [CreatedBy], [Name])
    VALUES (''OfficeSupplies'', NULL, N''Office Supplies''),
    (''Packaging'', NULL, N''Packaging''),
    (''Stationery'', NULL, N''Stationery'')');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'CategoryCode', N'CreatedBy', N'Name') AND [object_id] = OBJECT_ID(N'[ProductCategories]'))
        SET IDENTITY_INSERT [ProductCategories] OFF;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031173410_InitialCreate'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'RoleCode', N'CreatedBy', N'Name') AND [object_id] = OBJECT_ID(N'[Roles]'))
        SET IDENTITY_INSERT [Roles] ON;
    EXEC(N'INSERT INTO [Roles] ([RoleCode], [CreatedBy], [Name])
    VALUES (''Administrator'', NULL, N''Administrator''),
    (''Auditor'', NULL, N''Auditor''),
    (''BranchManager'', NULL, N''Branch Manager''),
    (''CounterClerk'', NULL, N''Counter Clerk'')');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'RoleCode', N'CreatedBy', N'Name') AND [object_id] = OBJECT_ID(N'[Roles]'))
        SET IDENTITY_INSERT [Roles] OFF;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031173410_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_AuditEntries_Timestamp_Branch] ON [AuditEntries] ([Timestamp], [BranchCode]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031173410_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_BranchStock_Branch_Quantity_Sku] ON [BranchStock] ([BranchCode], [Quantity], [Sku]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031173410_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_BranchStock_Sku] ON [BranchStock] ([Sku]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031173410_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Counters_BranchCode] ON [Counters] ([BranchCode]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031173410_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_CounterTransactions_Branch_Occurred_Status] ON [CounterTransactions] ([BranchCode], [OccurredAtUtc], [Status]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031173410_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_CounterTransactions_ClerkId] ON [CounterTransactions] ([ClerkId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031173410_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_CounterTransactions_CounterId] ON [CounterTransactions] ([CounterId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031173410_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_IdentityVerifications_MoneyOrderId] ON [IdentityVerifications] ([MoneyOrderId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031173410_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [UX_JsonImportRuns_SourceFingerprint] ON [JsonImportRuns] ([SourceFingerprint]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031173410_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_MoneyOrders_Branch_Created] ON [MoneyOrders] ([BranchCode], [CreatedAtUtc], [Id]) INCLUDE ([Amount], [Fee], [TransactionId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031173410_InitialCreate'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [UX_MoneyOrders_Transaction] ON [MoneyOrders] ([TransactionId]) WHERE [TransactionId] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031173410_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Products_Category] ON [Products] ([Category]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031173410_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Shipments_BranchCode] ON [Shipments] ([BranchCode]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031173410_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Shipments_CounterTransactionId] ON [Shipments] ([CounterTransactionId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031173410_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Shipments_Zone_Service_Created] ON [Shipments] ([ZoneCode], [ServiceLevel], [CreatedAtUtc]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031173410_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Staff_HomeBranchCode] ON [Staff] ([HomeBranchCode]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031173410_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Staff_Role] ON [Staff] ([Role]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031173410_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [UX_Staff_Email] ON [Staff] ([Email]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031173410_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_StockMovements_BranchCode] ON [StockMovements] ([BranchCode]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031173410_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_StockMovements_Sku] ON [StockMovements] ([Sku]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031173410_InitialCreate'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [UX_StockMovements_Reversal] ON [StockMovements] ([TransactionId], [MovementType]) WHERE [TransactionId] IS NOT NULL AND [MovementType] IN (''VoidRestore'',''RefundRestore'')');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031173410_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_TransactionLines_Transaction_ItemType] ON [TransactionLines] ([TransactionId], [ItemType]) INCLUDE ([LineTotal], [Quantity]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031173410_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [UX_TransactionLines_Transaction_LineNumber] ON [TransactionLines] ([TransactionId], [LineNumber]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031173410_InitialCreate'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261031173410_InitialCreate', N'10.0.14');
END;

COMMIT;
GO
BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031173907_AddSqlReportingAndStockProcedures'
)
BEGIN
    DROP INDEX [UX_StockMovements_Reversal] ON [StockMovements];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031173907_AddSqlReportingAndStockProcedures'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [UX_StockMovements_Reversal] ON [StockMovements] ([TransactionId]) WHERE [TransactionId] IS NOT NULL AND [MovementType] = ''ReversalRestore''');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031173907_AddSqlReportingAndStockProcedures'
)
BEGIN
    CREATE OR ALTER PROCEDURE dbo.usp_DailyBranchSales
        @ReportDate date,
        @BranchCode varchar(10) = NULL
    AS
    BEGIN
        SET NOCOUNT ON;
        SELECT BranchCode, CONVERT(date, OccurredAtUtc) AS SaleDate,
               COUNT_BIG(*) AS TransactionCount, SUM(Total) AS GrossSales
        FROM dbo.CounterTransactions
        WHERE Status = 'Paid' AND OccurredAtUtc >= @ReportDate
          AND OccurredAtUtc < DATEADD(day, 1, @ReportDate)
          AND (@BranchCode IS NULL OR BranchCode = @BranchCode)
        GROUP BY BranchCode, CONVERT(date, OccurredAtUtc);
    END
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031173907_AddSqlReportingAndStockProcedures'
)
BEGIN
    CREATE OR ALTER PROCEDURE dbo.usp_LowStockReport
        @ReorderLevel int = 5,
        @BranchCode varchar(10) = NULL
    AS
    BEGIN
        SET NOCOUNT ON;
        SELECT stock.BranchCode, stock.Sku, product.Name AS ProductName,
               stock.Quantity, @ReorderLevel AS ReorderLevel
        FROM dbo.BranchStock AS stock
        INNER JOIN dbo.Products AS product ON product.Sku = stock.Sku
        WHERE stock.Quantity <= @ReorderLevel
          AND (@BranchCode IS NULL OR stock.BranchCode = @BranchCode)
        ORDER BY stock.BranchCode, stock.Quantity, stock.Sku;
    END
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031173907_AddSqlReportingAndStockProcedures'
)
BEGIN
    CREATE OR ALTER PROCEDURE dbo.usp_AdjustStock
        @BranchCode varchar(10),
        @Sku varchar(20),
        @Delta int,
        @Reason nvarchar(500),
        @ChangedBy varchar(128)
    AS
    BEGIN
        SET NOCOUNT ON;
        SET XACT_ABORT ON;
        IF @Delta = 0 OR @Delta = -2147483648
            THROW 51000, 'Stock adjustment must be a non-zero supported quantity.', 1;
        IF NULLIF(LTRIM(RTRIM(@Reason)), '') IS NULL OR NULLIF(LTRIM(RTRIM(@ChangedBy)), '') IS NULL
            THROW 51001, 'A reason and actor are required for stock adjustments.', 1;

        BEGIN TRANSACTION;
        DECLARE @Before int;
        SELECT @Before = Quantity FROM dbo.BranchStock WITH (UPDLOCK, HOLDLOCK)
        WHERE BranchCode = @BranchCode AND Sku = @Sku;
        IF @Before IS NULL
        BEGIN
            IF @Delta < 0
            BEGIN
                ROLLBACK TRANSACTION;
                THROW 51002, 'Cannot reduce stock when no stock row exists.', 1;
            END;
            SET @Before = 0;
            INSERT dbo.BranchStock (BranchCode, Sku, Quantity, CreatedAtUtc, CreatedBy)
            VALUES (@BranchCode, @Sku, @Delta, SYSUTCDATETIME(), @ChangedBy);
        END
        ELSE
        BEGIN
            IF CONVERT(bigint, @Before) + @Delta < 0
            BEGIN
                ROLLBACK TRANSACTION;
                THROW 51003, 'Stock adjustment would make quantity negative.', 1;
            END;
            UPDATE dbo.BranchStock SET Quantity = @Before + @Delta,
                ModifiedAtUtc = SYSUTCDATETIME()
            WHERE BranchCode = @BranchCode AND Sku = @Sku;
        END;

        INSERT dbo.StockMovements
            (BranchCode, Sku, QuantityDelta, QuantityBefore, QuantityAfter,
             MovementType, Reason, ChangedBy, CreatedAtUtc)
        VALUES
            (@BranchCode, @Sku, @Delta, @Before, @Before + @Delta,
             'Adjustment', @Reason, @ChangedBy, SYSUTCDATETIME());
        COMMIT TRANSACTION;
        SELECT BranchCode, Sku, Quantity FROM dbo.BranchStock
        WHERE BranchCode = @BranchCode AND Sku = @Sku;
    END
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031173907_AddSqlReportingAndStockProcedures'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261031173907_AddSqlReportingAndStockProcedures', N'10.0.14');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031174229_SeedShippingZonesAndRates'
)
BEGIN
    DECLARE @Zones TABLE (Code varchar(32) NOT NULL PRIMARY KEY, Name nvarchar(100) NOT NULL);
    INSERT @Zones (Code, Name) VALUES
        ('ZONE1', N'Zone 1'), ('ZONE2', N'Zone 2'), ('ZONE3', N'Zone 3'),
        ('ZONE4', N'Zone 4'), ('ZONE5', N'Zone 5'), ('ZONE6', N'Zone 6'),
        ('ZONE7', N'Zone 7'), ('ZONE8', N'Zone 8'), ('ZONE9', N'Zone 9'),
        ('ZONE10', N'Zone 10');
    INSERT dbo.ShippingZones (Code, Name)
    SELECT source.Code, source.Name FROM @Zones AS source
    WHERE NOT EXISTS (SELECT 1 FROM dbo.ShippingZones AS target WHERE target.Code = source.Code);

    DECLARE @Rates TABLE
    (
        ZoneCode varchar(32) NOT NULL,
        ServiceLevel varchar(32) NOT NULL,
        BasePrice decimal(18,2) NOT NULL,
        PricePerKilogram decimal(18,2) NOT NULL,
        PRIMARY KEY (ZoneCode, ServiceLevel)
    );
    INSERT @Rates (ZoneCode, ServiceLevel, BasePrice, PricePerKilogram) VALUES
        ('ZONE1','Standard',40,20), ('ZONE1','Express',80,30), ('ZONE1','Registered',65,25),
        ('ZONE2','Standard',42,22), ('ZONE3','Standard',44,24),
        ('ZONE4','Standard',46,26), ('ZONE5','Standard',48,28),
        ('ZONE6','Standard',50,30), ('ZONE7','Standard',52,32),
        ('ZONE8','Standard',54,34), ('ZONE9','Standard',56,36),
        ('ZONE10','Standard',58,38);
    INSERT dbo.ShippingRates (ZoneCode, ServiceLevel, BasePrice, PricePerKilogram)
    SELECT source.ZoneCode, source.ServiceLevel, source.BasePrice, source.PricePerKilogram
    FROM @Rates AS source
    WHERE NOT EXISTS
    (
        SELECT 1 FROM dbo.ShippingRates AS target
        WHERE target.ZoneCode = source.ZoneCode AND target.ServiceLevel = source.ServiceLevel
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031174229_SeedShippingZonesAndRates'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261031174229_SeedShippingZonesAndRates', N'10.0.14');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031175835_GuardStockAdjustmentOverflow'
)
BEGIN
    CREATE OR ALTER PROCEDURE dbo.usp_AdjustStock
        @BranchCode varchar(10),
        @Sku varchar(20),
        @Delta int,
        @Reason nvarchar(500),
        @ChangedBy varchar(128)
    AS
    BEGIN
        SET NOCOUNT ON;
        SET XACT_ABORT ON;
        IF @Delta = 0 OR @Delta = -2147483648
            THROW 51000, 'Stock adjustment must be a non-zero supported quantity.', 1;
        IF NULLIF(LTRIM(RTRIM(@Reason)), '') IS NULL OR NULLIF(LTRIM(RTRIM(@ChangedBy)), '') IS NULL
            THROW 51001, 'A reason and actor are required for stock adjustments.', 1;

        BEGIN TRANSACTION;
        DECLARE @Before int;
        SELECT @Before = Quantity FROM dbo.BranchStock WITH (UPDLOCK, HOLDLOCK)
        WHERE BranchCode = @BranchCode AND Sku = @Sku;
        IF @Before IS NULL
        BEGIN
            IF @Delta < 0
            BEGIN
                ROLLBACK TRANSACTION;
                THROW 51002, 'Cannot reduce stock when no stock row exists.', 1;
            END;
            SET @Before = 0;
            INSERT dbo.BranchStock (BranchCode, Sku, Quantity, CreatedAtUtc, CreatedBy)
            VALUES (@BranchCode, @Sku, @Delta, SYSUTCDATETIME(), @ChangedBy);
        END
        ELSE
        BEGIN
            IF CONVERT(bigint, @Before) + @Delta < 0
            BEGIN
                ROLLBACK TRANSACTION;
                THROW 51003, 'Stock adjustment would make quantity negative.', 1;
            END;
            IF CONVERT(bigint, @Before) + @Delta > 2147483647
            BEGIN
                ROLLBACK TRANSACTION;
                THROW 51004, 'Stock adjustment exceeds the supported quantity range.', 1;
            END;
            UPDATE dbo.BranchStock SET Quantity = @Before + @Delta,
                ModifiedAtUtc = SYSUTCDATETIME()
            WHERE BranchCode = @BranchCode AND Sku = @Sku;
        END;

        INSERT dbo.StockMovements
            (BranchCode, Sku, QuantityDelta, QuantityBefore, QuantityAfter,
             MovementType, Reason, ChangedBy, CreatedAtUtc)
        VALUES
            (@BranchCode, @Sku, @Delta, @Before, @Before + @Delta,
             'Adjustment', @Reason, @ChangedBy, SYSUTCDATETIME());
        COMMIT TRANSACTION;
        SELECT BranchCode, Sku, Quantity FROM dbo.BranchStock
        WHERE BranchCode = @BranchCode AND Sku = @Sku;
    END
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261031175835_GuardStockAdjustmentOverflow'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261031175835_GuardStockAdjustmentOverflow', N'10.0.14');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101010000_UpdateDailyBranchSalesProcedure'
)
BEGIN
    CREATE OR ALTER PROCEDURE dbo.usp_DailyBranchSales
        @ReportDate date,
        @BranchCode varchar(10) = NULL
    AS
    BEGIN
        SET NOCOUNT ON;
        IF @ReportDate IS NULL
            THROW 51005, 'A report date is required.', 1;
        IF @BranchCode IS NOT NULL AND NULLIF(LTRIM(RTRIM(@BranchCode)), '') IS NULL
            THROW 51006, 'Branch code cannot be blank.', 1;

        SELECT transactionRow.BranchCode,
               CONVERT(date, transactionRow.OccurredAtUtc) AS SaleDate,
               COUNT_BIG(DISTINCT transactionRow.Id) AS TransactionCount,
               COALESCE(SUM(CASE WHEN line.ItemType = 'Retail'
                    THEN line.LineTotal + line.LineTotal * tax.Rate ELSE 0 END), 0) AS RetailSales,
               COALESCE(SUM(CASE WHEN line.ItemType = 'Service'
                    THEN line.LineTotal + line.LineTotal * tax.Rate ELSE 0 END), 0) AS ServiceSales,
               COALESCE(SUM(line.LineTotal + line.LineTotal * tax.Rate), 0) AS TotalSales
        FROM dbo.CounterTransactions AS transactionRow
        INNER JOIN dbo.TransactionLines AS line ON line.TransactionId = transactionRow.Id
        INNER JOIN dbo.TransactionTaxRates AS tax
            ON tax.TransactionId = line.TransactionId AND tax.Category = line.TaxCategory
        WHERE transactionRow.Status = 'Paid'
          AND transactionRow.OccurredAtUtc >= @ReportDate
          AND transactionRow.OccurredAtUtc < DATEADD(day, 1, @ReportDate)
          AND line.ItemType IN ('Retail', 'Service')
          AND (@BranchCode IS NULL OR transactionRow.BranchCode = @BranchCode)
        GROUP BY transactionRow.BranchCode, CONVERT(date, transactionRow.OccurredAtUtc);
    END
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101010000_UpdateDailyBranchSalesProcedure'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261101010000_UpdateDailyBranchSalesProcedure', N'10.0.14');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102000000_AddReportFilters'
)
BEGIN
    CREATE OR ALTER PROCEDURE dbo.usp_DailyBranchSales
        @BranchCode varchar(10) = NULL,
        @Date date
    AS
    BEGIN
        SET NOCOUNT ON;

        IF @Date IS NULL
            THROW 51005, 'A report date is required.', 1;
        IF @BranchCode IS NOT NULL AND NULLIF(LTRIM(RTRIM(@BranchCode)), '') IS NULL
            THROW 51006, 'Branch code cannot be blank.', 1;

        SELECT transactionRow.BranchCode,
               CONVERT(date, transactionRow.OccurredAtUtc) AS SaleDate,
               COUNT_BIG(DISTINCT transactionRow.Id) AS TransactionCount,
               COALESCE(SUM(CASE WHEN line.ItemType = 'Retail'
                    THEN line.LineTotal + line.LineTotal * tax.Rate ELSE 0 END), 0) AS RetailSales,
               COALESCE(SUM(CASE WHEN line.ItemType = 'Service'
                    THEN line.LineTotal + line.LineTotal * tax.Rate ELSE 0 END), 0) AS ServiceSales,
               COALESCE(SUM(CASE WHEN line.ItemType IN ('Retail', 'Service')
                    THEN line.LineTotal + line.LineTotal * tax.Rate ELSE 0 END), 0) AS TotalSales
        FROM dbo.CounterTransactions AS transactionRow
        INNER JOIN dbo.TransactionLines AS line
            ON line.TransactionId = transactionRow.Id
        INNER JOIN dbo.TransactionTaxRates AS tax
            ON tax.TransactionId = line.TransactionId
           AND tax.Category = line.TaxCategory
        WHERE transactionRow.Status = 'Paid'
          AND transactionRow.OccurredAtUtc >= @Date
          AND transactionRow.OccurredAtUtc < DATEADD(day, 1, @Date)
          AND line.ItemType IN ('Retail', 'Service')
          AND (@BranchCode IS NULL OR transactionRow.BranchCode = @BranchCode)
        GROUP BY transactionRow.BranchCode, CONVERT(date, transactionRow.OccurredAtUtc);
    END
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102000000_AddReportFilters'
)
BEGIN
    CREATE OR ALTER PROCEDURE dbo.usp_LowStockReport
        @BranchCode varchar(10) = NULL,
        @Threshold int = 5
    AS
    BEGIN
        SET NOCOUNT ON;

        IF @Threshold IS NULL OR @Threshold < 0 OR @Threshold > 100000
            THROW 51007, 'Threshold must be between 0 and 100000.', 1;
        IF @BranchCode IS NOT NULL AND NULLIF(LTRIM(RTRIM(@BranchCode)), '') IS NULL
            THROW 51008, 'Branch code cannot be blank.', 1;

        SELECT stock.BranchCode, stock.Sku, product.Name AS ProductName,
               stock.Quantity, @Threshold AS Threshold
        FROM dbo.BranchStock AS stock
        INNER JOIN dbo.Products AS product ON product.Sku = stock.Sku
        WHERE stock.Quantity <= @Threshold
          AND (@BranchCode IS NULL OR stock.BranchCode = @BranchCode)
        ORDER BY stock.BranchCode, stock.Quantity, stock.Sku
        OPTION (RECOMPILE);
    END
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102000000_AddReportFilters'
)
BEGIN
    CREATE OR ALTER PROCEDURE dbo.usp_AdjustStock
        @BranchCode varchar(10),
        @Sku varchar(20),
        @Delta int,
        @Reason nvarchar(500),
        @ChangedBy varchar(128)
    AS
    BEGIN
        SET NOCOUNT ON;
        SET XACT_ABORT ON;

        IF NULLIF(LTRIM(RTRIM(@BranchCode)), '') IS NULL
           OR NULLIF(LTRIM(RTRIM(@Sku)), '') IS NULL
            THROW 51009, 'Branch code and SKU are required.', 1;
        IF @Delta = 0 OR @Delta = -2147483648
            THROW 51000, 'Stock adjustment must be a non-zero supported quantity.', 1;
        IF NULLIF(LTRIM(RTRIM(@Reason)), '') IS NULL
           OR NULLIF(LTRIM(RTRIM(@ChangedBy)), '') IS NULL
            THROW 51001, 'A reason and actor are required for stock adjustments.', 1;

        BEGIN TRY
            BEGIN TRANSACTION;

            DECLARE @Before int;
            SELECT @Before = Quantity
            FROM dbo.BranchStock WITH (UPDLOCK, HOLDLOCK)
            WHERE BranchCode = @BranchCode AND Sku = @Sku;

            IF @Before IS NULL
            BEGIN
                IF @Delta < 0
                    THROW 51002, 'Cannot reduce stock when no stock row exists.', 1;

                SET @Before = 0;
                INSERT dbo.BranchStock (BranchCode, Sku, Quantity, CreatedAtUtc, CreatedBy)
                VALUES (@BranchCode, @Sku, @Delta, SYSUTCDATETIME(), @ChangedBy);
            END
            ELSE
            BEGIN
                IF CONVERT(bigint, @Before) + @Delta < 0
                    THROW 51003, 'Stock adjustment would make quantity negative.', 1;
                IF CONVERT(bigint, @Before) + @Delta > 2147483647
                    THROW 51004, 'Stock adjustment exceeds the supported quantity range.', 1;

                UPDATE dbo.BranchStock
                SET Quantity = @Before + @Delta,
                    ModifiedAtUtc = SYSUTCDATETIME()
                WHERE BranchCode = @BranchCode AND Sku = @Sku;
            END;

            INSERT dbo.StockMovements
                (BranchCode, Sku, QuantityDelta, QuantityBefore, QuantityAfter,
                 MovementType, Reason, ChangedBy, CreatedAtUtc)
            VALUES
                (@BranchCode, @Sku, @Delta, @Before, @Before + @Delta,
                 'Adjustment', @Reason, @ChangedBy, SYSUTCDATETIME());

            COMMIT TRANSACTION;
            SELECT BranchCode, Sku, Quantity
            FROM dbo.BranchStock
            WHERE BranchCode = @BranchCode AND Sku = @Sku;
        END TRY
        BEGIN CATCH
            IF XACT_STATE() <> 0
                ROLLBACK TRANSACTION;
            THROW;
        END CATCH;
    END
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261102000000_AddReportFilters'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261102000000_AddReportFilters', N'10.0.14');
END;

COMMIT;
GO

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
    WHERE [MigrationId] = N'20261008132350_InitialCreate'
)
BEGIN
    CREATE TABLE [Products] (
        [ProductId] int NOT NULL IDENTITY,
        [ProductName] nvarchar(150) NOT NULL,
        [Description] nvarchar(1000) NOT NULL,
        [Category] nvarchar(50) NOT NULL,
        [StartingPrice] decimal(10,2) NOT NULL,
        [Available] bit NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_Products] PRIMARY KEY ([ProductId])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261008132350_InitialCreate'
)
BEGIN
    CREATE TABLE [UserAccounts] (
        [UserId] int NOT NULL IDENTITY,
        [FullName] nvarchar(100) NOT NULL,
        [Email] nvarchar(150) NOT NULL,
        [PhoneNumber] nvarchar(20) NOT NULL,
        [PasswordHash] nvarchar(max) NOT NULL,
        [Role] nvarchar(20) NOT NULL DEFAULT N'Customer',
        [Active] bit NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_UserAccounts] PRIMARY KEY ([UserId])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261008132350_InitialCreate'
)
BEGIN
    CREATE TABLE [ProductImages] (
        [ProductImageId] int NOT NULL IDENTITY,
        [ProductId] int NOT NULL,
        [ImageUrl] nvarchar(500) NOT NULL,
        [IsPrimary] bit NOT NULL,
        [IsGalleryImage] bit NOT NULL,
        CONSTRAINT [PK_ProductImages] PRIMARY KEY ([ProductImageId]),
        CONSTRAINT [FK_ProductImages_Products_ProductId] FOREIGN KEY ([ProductId]) REFERENCES [Products] ([ProductId]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261008132350_InitialCreate'
)
BEGIN
    CREATE TABLE [CustomCakeRequests] (
        [CustomCakeRequestId] int NOT NULL IDENTITY,
        [CustomerId] int NOT NULL,
        [ConsultantId] int NULL,
        [CakeSize] nvarchar(50) NOT NULL,
        [Flavour] nvarchar(100) NOT NULL,
        [Filling] nvarchar(100) NULL,
        [CustomizationDetails] nvarchar(1000) NULL,
        [CollectionDateTime] datetime2 NOT NULL,
        [Status] nvarchar(30) NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_CustomCakeRequests] PRIMARY KEY ([CustomCakeRequestId]),
        CONSTRAINT [FK_CustomCakeRequests_UserAccounts_ConsultantId] FOREIGN KEY ([ConsultantId]) REFERENCES [UserAccounts] ([UserId]) ON DELETE NO ACTION,
        CONSTRAINT [FK_CustomCakeRequests_UserAccounts_CustomerId] FOREIGN KEY ([CustomerId]) REFERENCES [UserAccounts] ([UserId]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261008132350_InitialCreate'
)
BEGIN
    CREATE TABLE [Enquiries] (
        [EnquiryId] int NOT NULL IDENTITY,
        [CustomerId] int NULL,
        [Name] nvarchar(100) NOT NULL,
        [Email] nvarchar(150) NOT NULL,
        [Subject] nvarchar(200) NOT NULL,
        [Message] nvarchar(2000) NOT NULL,
        [Status] nvarchar(30) NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_Enquiries] PRIMARY KEY ([EnquiryId]),
        CONSTRAINT [FK_Enquiries_UserAccounts_CustomerId] FOREIGN KEY ([CustomerId]) REFERENCES [UserAccounts] ([UserId]) ON DELETE SET NULL
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261008132350_InitialCreate'
)
BEGIN
    CREATE TABLE [UserSessions] (
        [SessionId] int NOT NULL IDENTITY,
        [UserId] int NOT NULL,
        [RefreshTokenHash] nvarchar(max) NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [ExpiresAt] datetime2 NOT NULL,
        [RevokedAt] datetime2 NULL,
        CONSTRAINT [PK_UserSessions] PRIMARY KEY ([SessionId]),
        CONSTRAINT [FK_UserSessions_UserAccounts_UserId] FOREIGN KEY ([UserId]) REFERENCES [UserAccounts] ([UserId]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261008132350_InitialCreate'
)
BEGIN
    CREATE TABLE [CustomCakeImages] (
        [CustomCakeImageId] int NOT NULL IDENTITY,
        [CustomCakeRequestId] int NOT NULL,
        [ImageUrl] nvarchar(500) NOT NULL,
        [UploadedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_CustomCakeImages] PRIMARY KEY ([CustomCakeImageId]),
        CONSTRAINT [FK_CustomCakeImages_CustomCakeRequests_CustomCakeRequestId] FOREIGN KEY ([CustomCakeRequestId]) REFERENCES [CustomCakeRequests] ([CustomCakeRequestId]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261008132350_InitialCreate'
)
BEGIN
    CREATE TABLE [Quotations] (
        [QuotationId] int NOT NULL IDENTITY,
        [CustomCakeRequestId] int NOT NULL,
        [ConsultantId] int NOT NULL,
        [QuotedAmount] decimal(10,2) NOT NULL,
        [Notes] nvarchar(1000) NULL,
        [Status] nvarchar(30) NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [ExpiresAt] datetime2 NULL,
        CONSTRAINT [PK_Quotations] PRIMARY KEY ([QuotationId]),
        CONSTRAINT [FK_Quotations_CustomCakeRequests_CustomCakeRequestId] FOREIGN KEY ([CustomCakeRequestId]) REFERENCES [CustomCakeRequests] ([CustomCakeRequestId]) ON DELETE NO ACTION,
        CONSTRAINT [FK_Quotations_UserAccounts_ConsultantId] FOREIGN KEY ([ConsultantId]) REFERENCES [UserAccounts] ([UserId]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261008132350_InitialCreate'
)
BEGIN
    CREATE TABLE [CustomerOrders] (
        [OrderId] int NOT NULL IDENTITY,
        [CustomerId] int NOT NULL,
        [QuotationId] int NULL,
        [TotalAmount] decimal(10,2) NOT NULL,
        [OrderStatus] nvarchar(30) NOT NULL,
        [CollectionDateTime] datetime2 NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_CustomerOrders] PRIMARY KEY ([OrderId]),
        CONSTRAINT [FK_CustomerOrders_Quotations_QuotationId] FOREIGN KEY ([QuotationId]) REFERENCES [Quotations] ([QuotationId]) ON DELETE NO ACTION,
        CONSTRAINT [FK_CustomerOrders_UserAccounts_CustomerId] FOREIGN KEY ([CustomerId]) REFERENCES [UserAccounts] ([UserId]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261008132350_InitialCreate'
)
BEGIN
    CREATE TABLE [OrderItems] (
        [OrderItemId] int NOT NULL IDENTITY,
        [OrderId] int NOT NULL,
        [ProductId] int NOT NULL,
        [Quantity] int NOT NULL,
        [UnitPrice] decimal(10,2) NOT NULL,
        CONSTRAINT [PK_OrderItems] PRIMARY KEY ([OrderItemId]),
        CONSTRAINT [FK_OrderItems_CustomerOrders_OrderId] FOREIGN KEY ([OrderId]) REFERENCES [CustomerOrders] ([OrderId]) ON DELETE CASCADE,
        CONSTRAINT [FK_OrderItems_Products_ProductId] FOREIGN KEY ([ProductId]) REFERENCES [Products] ([ProductId]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261008132350_InitialCreate'
)
BEGIN
    CREATE TABLE [OrderStatusHistories] (
        [OrderStatusHistoryId] int NOT NULL IDENTITY,
        [OrderId] int NOT NULL,
        [Status] nvarchar(30) NOT NULL,
        [Notes] nvarchar(500) NULL,
        [ChangedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_OrderStatusHistories] PRIMARY KEY ([OrderStatusHistoryId]),
        CONSTRAINT [FK_OrderStatusHistories_CustomerOrders_OrderId] FOREIGN KEY ([OrderId]) REFERENCES [CustomerOrders] ([OrderId]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261008132350_InitialCreate'
)
BEGIN
    CREATE TABLE [Payments] (
        [PaymentId] int NOT NULL IDENTITY,
        [OrderId] int NOT NULL,
        [Amount] decimal(10,2) NOT NULL,
        [PaymentMethod] nvarchar(30) NOT NULL,
        [PaymentStatus] nvarchar(30) NOT NULL,
        [PaymentReference] nvarchar(200) NULL,
        [ProofOfPaymentUrl] nvarchar(500) NULL,
        [PaidAt] datetime2 NULL,
        [CreatedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_Payments] PRIMARY KEY ([PaymentId]),
        CONSTRAINT [FK_Payments_CustomerOrders_OrderId] FOREIGN KEY ([OrderId]) REFERENCES [CustomerOrders] ([OrderId]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261008132350_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_CustomCakeImages_CustomCakeRequestId] ON [CustomCakeImages] ([CustomCakeRequestId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261008132350_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_CustomCakeRequests_ConsultantId] ON [CustomCakeRequests] ([ConsultantId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261008132350_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_CustomCakeRequests_CustomerId] ON [CustomCakeRequests] ([CustomerId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261008132350_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_CustomerOrders_CustomerId] ON [CustomerOrders] ([CustomerId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261008132350_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_CustomerOrders_QuotationId] ON [CustomerOrders] ([QuotationId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261008132350_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Enquiries_CustomerId] ON [Enquiries] ([CustomerId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261008132350_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_OrderItems_OrderId] ON [OrderItems] ([OrderId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261008132350_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_OrderItems_ProductId] ON [OrderItems] ([ProductId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261008132350_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_OrderStatusHistories_OrderId] ON [OrderStatusHistories] ([OrderId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261008132350_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Payments_OrderId] ON [Payments] ([OrderId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261008132350_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_ProductImages_ProductId] ON [ProductImages] ([ProductId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261008132350_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Quotations_ConsultantId] ON [Quotations] ([ConsultantId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261008132350_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Quotations_CustomCakeRequestId] ON [Quotations] ([CustomCakeRequestId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261008132350_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_UserAccounts_Email] ON [UserAccounts] ([Email]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261008132350_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_UserSessions_UserId] ON [UserSessions] ([UserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261008132350_InitialCreate'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261008132350_InitialCreate', N'10.0.12');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261011133231_AddCustomCakeImageStoredFileName'
)
BEGIN
    ALTER TABLE [CustomCakeImages] ADD [StoredFileName] nvarchar(255) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261011133231_AddCustomCakeImageStoredFileName'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261011133231_AddCustomCakeImageStoredFileName', N'10.0.12');
END;

COMMIT;
GO


BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261015071335_WebsiteIntegration'
)
BEGIN
    DROP INDEX [IX_CustomerOrders_QuotationId] ON [CustomerOrders];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261015071335_WebsiteIntegration'
)
BEGIN
    ALTER TABLE [Products] ADD [Image] nvarchar(500) NOT NULL DEFAULT N'';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261015071335_WebsiteIntegration'
)
BEGIN
    ALTER TABLE [Products] ADD [Servings] nvarchar(100) NOT NULL DEFAULT N'';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261015071335_WebsiteIntegration'
)
BEGIN
    ALTER TABLE [Products] ADD [Size] nvarchar(100) NOT NULL DEFAULT N'';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261015071335_WebsiteIntegration'
)
BEGIN
    ALTER TABLE [Payments] ADD [StoredProofName] nvarchar(255) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261015071335_WebsiteIntegration'
)
BEGIN
    ALTER TABLE [CustomerOrders] ADD [CancelRequested] bit NOT NULL DEFAULT CAST(0 AS bit);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261015071335_WebsiteIntegration'
)
BEGIN
    ALTER TABLE [CustomerOrders] ADD [ContactName] nvarchar(100) NOT NULL DEFAULT N'';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261015071335_WebsiteIntegration'
)
BEGIN
    ALTER TABLE [CustomerOrders] ADD [ContactPhone] nvarchar(20) NOT NULL DEFAULT N'';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261015071335_WebsiteIntegration'
)
BEGIN
    ALTER TABLE [CustomerOrders] ADD [Instructions] nvarchar(1000) NOT NULL DEFAULT N'';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261015071335_WebsiteIntegration'
)
BEGIN
    ALTER TABLE [CustomerOrders] ADD [RequestId] int NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261015071335_WebsiteIntegration'
)
BEGIN
    ALTER TABLE [CustomerOrders] ADD [SlotApproved] bit NOT NULL DEFAULT CAST(0 AS bit);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261015071335_WebsiteIntegration'
)
BEGIN
    ALTER TABLE [CustomerOrders] ADD [Version] rowversion NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261015071335_WebsiteIntegration'
)
BEGIN
    ALTER TABLE [CustomCakeRequests] ADD [ContactName] nvarchar(100) NOT NULL DEFAULT N'';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261015071335_WebsiteIntegration'
)
BEGIN
    ALTER TABLE [CustomCakeRequests] ADD [ContactPhone] nvarchar(20) NOT NULL DEFAULT N'';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261015071335_WebsiteIntegration'
)
BEGIN
    CREATE TABLE [BlockedCollectionDates] (
        [Id] int NOT NULL IDENTITY,
        [Date] nvarchar(10) NOT NULL,
        CONSTRAINT [PK_BlockedCollectionDates] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261015071335_WebsiteIntegration'
)
BEGIN
    CREATE TABLE [CustomerReviews] (
        [Id] int NOT NULL IDENTITY,
        [CustomerId] int NOT NULL,
        [Name] nvarchar(60) NOT NULL,
        [Rating] int NOT NULL,
        [Message] nvarchar(1000) NOT NULL,
        [Status] nvarchar(20) NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_CustomerReviews] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261015071335_WebsiteIntegration'
)
BEGIN

                    UPDATE UserAccounts SET Role = 'Staff' WHERE Role = 'Consultant';
                    UPDATE CustomCakeRequests SET Status = CASE Status
                        WHEN 'Pending' THEN 'Submitted' WHEN 'Under Review' THEN 'Under review'
                        WHEN 'Rejected' THEN 'Declined' ELSE Status END;
                    UPDATE Quotations SET Status = 'Quoted' WHERE Status = 'Pending';
                    UPDATE CustomCakeRequests SET Status = 'Accepted' WHERE EXISTS (
                        SELECT 1 FROM Quotations q WHERE q.CustomCakeRequestId = CustomCakeRequests.CustomCakeRequestId AND q.Status = 'Accepted');
                    UPDATE CustomerOrders SET OrderStatus = CASE OrderStatus
                        WHEN 'Pending' THEN 'Awaiting confirmation' WHEN 'Pending Deposit' THEN 'Awaiting confirmation'
                        WHEN 'Deposit Paid' THEN 'Awaiting confirmation' WHEN 'Paid' THEN 'Awaiting confirmation'
                        WHEN 'In Preparation' THEN 'In progress' WHEN 'Ready for Collection' THEN 'Ready for collection'
                        ELSE OrderStatus END;
                    UPDATE Enquiries SET Status = CASE Status WHEN 'Pending' THEN 'New' WHEN 'In Progress' THEN 'In progress' ELSE Status END;
                    UPDATE o SET ContactName = u.FullName, ContactPhone = u.PhoneNumber,
                        SlotApproved = CASE WHEN OrderStatus IN ('In progress', 'Ready for collection', 'Collected') THEN 1 ELSE 0 END
                        FROM CustomerOrders o JOIN UserAccounts u ON u.UserId = o.CustomerId;
                    UPDATE r SET ContactName = u.FullName, ContactPhone = u.PhoneNumber
                        FROM CustomCakeRequests r JOIN UserAccounts u ON u.UserId = r.CustomerId;
                    UPDATE o SET RequestId = q.CustomCakeRequestId FROM CustomerOrders o JOIN Quotations q ON q.QuotationId = o.QuotationId;
                    UPDATE Payments SET PaymentReference = NULLIF(UPPER(LTRIM(RTRIM(PaymentReference))), '');
                    IF EXISTS (SELECT PaymentReference FROM Payments WHERE PaymentReference IS NOT NULL GROUP BY PaymentReference HAVING COUNT(*) > 1)
                        THROW 51000, 'Resolve duplicate historical payment references before applying WebsiteIntegration.', 1;
                    IF EXISTS (SELECT RequestId FROM CustomerOrders WHERE RequestId IS NOT NULL GROUP BY RequestId HAVING COUNT(*) > 1)
                        THROW 51001, 'Resolve duplicate orders for the same cake request before applying WebsiteIntegration.', 1;

END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261015071335_WebsiteIntegration'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_Payments_PaymentReference] ON [Payments] ([PaymentReference]) WHERE [PaymentReference] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261015071335_WebsiteIntegration'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_CustomerOrders_QuotationId] ON [CustomerOrders] ([QuotationId]) WHERE [QuotationId] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261015071335_WebsiteIntegration'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_CustomerOrders_RequestId] ON [CustomerOrders] ([RequestId]) WHERE [RequestId] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261015071335_WebsiteIntegration'
)
BEGIN
    CREATE UNIQUE INDEX [IX_BlockedCollectionDates_Date] ON [BlockedCollectionDates] ([Date]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261015071335_WebsiteIntegration'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261015071335_WebsiteIntegration', N'10.0.12');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261015074535_RetrySafeSubmissions'
)
BEGIN
    CREATE TABLE [WebsiteSubmissions] (
        [Id] uniqueidentifier NOT NULL,
        [CustomerId] int NOT NULL,
        [Kind] nvarchar(20) NOT NULL,
        [PayloadHash] nvarchar(64) NOT NULL,
        CONSTRAINT [PK_WebsiteSubmissions] PRIMARY KEY ([CustomerId], [Kind], [Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261015074535_RetrySafeSubmissions'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261015074535_RetrySafeSubmissions', N'10.0.12');
END;

COMMIT;
GO

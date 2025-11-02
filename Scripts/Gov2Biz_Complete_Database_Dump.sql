-- =====================================================
-- Gov2Biz Complete Database Dump
-- Generated: November 2, 2025
-- Database: Gov2Biz
-- Description: Complete schema with tables, stored procedures, and constraints
-- =====================================================

USE master;
GO

-- Drop database if exists (use with caution!)
-- IF EXISTS (SELECT name FROM sys.databases WHERE name = 'Gov2Biz')
-- BEGIN
--     ALTER DATABASE Gov2Biz SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
--     DROP DATABASE Gov2Biz;
-- END
-- GO

-- Create database
IF NOT EXISTS (SELECT name FROM sys.databases WHERE name = 'Gov2Biz')
BEGIN
    CREATE DATABASE Gov2Biz;
END
GO

USE Gov2Biz;
GO

-- =====================================================
-- TABLE: Users
-- =====================================================
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Users')
BEGIN
    CREATE TABLE Users (
        Id INT IDENTITY(1,1) PRIMARY KEY,
        Username NVARCHAR(100) NOT NULL UNIQUE,
        Email NVARCHAR(255) NOT NULL UNIQUE,
        PasswordHash NVARCHAR(500) NOT NULL,
        Roles NVARCHAR(200) NOT NULL DEFAULT 'User',
        TenantId NVARCHAR(50) NOT NULL,
        IsActive BIT NOT NULL DEFAULT 1,
        CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
        UpdatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
        LastLoginAt DATETIME2 NULL
    );
    PRINT 'Table Users created successfully.';
END
GO

-- =====================================================
-- TABLE: Licenses
-- =====================================================
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Licenses')
BEGIN
    CREATE TABLE Licenses (
        Id INT IDENTITY(1,1) PRIMARY KEY,
        LicenseNumber NVARCHAR(50) NOT NULL UNIQUE,
        TenantId NVARCHAR(50) NOT NULL,
        UserId INT NULL,
        ApplicantName NVARCHAR(200) NOT NULL,
        ApplicantEmail NVARCHAR(255) NOT NULL,
        LicenseType NVARCHAR(100) NOT NULL,
        Status NVARCHAR(50) NOT NULL DEFAULT 'Pending',
        IssueDate DATETIME2 NULL,
        ExpiryDate DATETIME2 NOT NULL,
        Amount DECIMAL(18,2) NOT NULL,
        PaymentId INT NULL,
        Metadata NVARCHAR(MAX) NULL,
        CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
        UpdatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
        CONSTRAINT FK_Licenses_Users FOREIGN KEY (UserId) REFERENCES Users(Id)
    );
    PRINT 'Table Licenses created successfully.';
END
GO

-- =====================================================
-- TABLE: LicenseHistory
-- =====================================================
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'LicenseHistory')
BEGIN
    CREATE TABLE LicenseHistory (
        Id INT IDENTITY(1,1) PRIMARY KEY,
        LicenseId INT NOT NULL,
        Timestamp DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
        Action NVARCHAR(100) NOT NULL,
        PerformedBy NVARCHAR(200) NOT NULL,
        Details NVARCHAR(MAX) NULL,
        CONSTRAINT FK_LicenseHistory_Licenses FOREIGN KEY (LicenseId) REFERENCES Licenses(Id) ON DELETE CASCADE
    );
    PRINT 'Table LicenseHistory created successfully.';
END
GO

-- =====================================================
-- TABLE: Payments
-- =====================================================
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Payments')
BEGIN
    CREATE TABLE Payments (
        Id INT IDENTITY(1,1) PRIMARY KEY,
        PaymentReference NVARCHAR(200) NOT NULL UNIQUE,
        Amount DECIMAL(18,2) NOT NULL,
        Currency NVARCHAR(10) NOT NULL DEFAULT 'INR',
        Status NVARCHAR(50) NOT NULL DEFAULT 'Pending',
        Metadata NVARCHAR(MAX) NULL,
        TenantId NVARCHAR(50) NOT NULL,
        CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
        UpdatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
        InvoiceId NVARCHAR(100) NULL,
        RazorpayOrderId NVARCHAR(100) NULL,
        RazorpayPaymentId NVARCHAR(100) NULL,
        RazorpaySignature NVARCHAR(500) NULL
    );
    PRINT 'Table Payments created successfully.';
END
GO

-- =====================================================
-- TABLE: Documents
-- =====================================================
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Documents')
BEGIN
    CREATE TABLE Documents (
        Id INT IDENTITY(1,1) PRIMARY KEY,
        LicenseId INT NOT NULL,
        FileName NVARCHAR(255) NOT NULL,
        FilePath NVARCHAR(500) NOT NULL,
        ContentType NVARCHAR(100) NOT NULL,
        FileSizeBytes BIGINT NOT NULL,
        TenantId NVARCHAR(50) NOT NULL,
        UploadedBy NVARCHAR(200) NOT NULL,
        UploadedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
        CONSTRAINT FK_Documents_Licenses FOREIGN KEY (LicenseId) REFERENCES Licenses(Id) ON DELETE CASCADE
    );
    PRINT 'Table Documents created successfully.';
END
GO

-- =====================================================
-- TABLE: Notifications
-- =====================================================
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Notifications')
BEGIN
    CREATE TABLE Notifications (
        Id INT IDENTITY(1,1) PRIMARY KEY,
        UserId INT NOT NULL,
        TenantId NVARCHAR(50) NOT NULL,
        Type NVARCHAR(50) NOT NULL,
        Title NVARCHAR(200) NOT NULL,
        Message NVARCHAR(MAX) NOT NULL,
        IsRead BIT NOT NULL DEFAULT 0,
        RelatedEntityId INT NULL,
        RelatedEntityType NVARCHAR(50) NULL,
        CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
        ReadAt DATETIME2 NULL,
        CONSTRAINT FK_Notifications_Users FOREIGN KEY (UserId) REFERENCES Users(Id) ON DELETE CASCADE
    );
    PRINT 'Table Notifications created successfully.';
END
GO

-- =====================================================
-- STORED PROCEDURE: sp_CreatePaymentWithInvoice
-- =====================================================
CREATE OR ALTER PROCEDURE sp_CreatePaymentWithInvoice
    @LicenseId INT,
    @Amount DECIMAL(18,2),
    @PaymentMethod NVARCHAR(100),
    @TenantId NVARCHAR(50),
    @RazorpayOrderId NVARCHAR(100) = NULL,
    @NewPaymentId INT OUTPUT,
    @InvoiceId NVARCHAR(100) OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    
    -- Generate Invoice ID: INV-YYYYMMDD-XXXXXX
    DECLARE @DatePart NVARCHAR(8) = CONVERT(NVARCHAR(8), GETUTCDATE(), 112);
    DECLARE @RandomPart NVARCHAR(6) = RIGHT('000000' + CAST(ABS(CHECKSUM(NEWID())) % 1000000 AS NVARCHAR(6)), 6);
    SET @InvoiceId = 'INV-' + @DatePart + '-' + @RandomPart;
    
    -- Generate unique payment reference
    -- For pre-payment (LicenseId = 0), use timestamp-based reference
    -- For post-payment, use license ID
    DECLARE @PaymentRef NVARCHAR(200);
    IF @LicenseId = 0
        SET @PaymentRef = 'PRE_' + @DatePart + '-' + @RandomPart;
    ELSE
        SET @PaymentRef = 'LIC_' + CAST(@LicenseId AS NVARCHAR(50));
    
    -- Create metadata JSON string
    DECLARE @MetadataJson NVARCHAR(MAX) = '{"licenseId": ' + CAST(@LicenseId AS NVARCHAR(10)) + ', "paymentMethod": "' + @PaymentMethod + '"}';
    
    INSERT INTO Payments (
        PaymentReference,
        Amount,
        Currency,
        Status,
        Metadata,
        InvoiceId,
        RazorpayOrderId,
        TenantId,
        CreatedAt,
        UpdatedAt
    )
    VALUES (
        @PaymentRef,
        @Amount,
        'INR',
        'Pending',
        @MetadataJson,
        @InvoiceId,
        @RazorpayOrderId,
        @TenantId,
        GETUTCDATE(),
        GETUTCDATE()
    );
    
    SET @NewPaymentId = SCOPE_IDENTITY();
    
    -- Link payment to license (only if LicenseId > 0)
    IF @LicenseId > 0
    BEGIN
        UPDATE Licenses 
        SET PaymentId = @NewPaymentId, UpdatedAt = GETUTCDATE()
        WHERE Id = @LicenseId;
    END
END
GO

-- =====================================================
-- STORED PROCEDURE: sp_CompletePayment
-- =====================================================
CREATE OR ALTER PROCEDURE sp_CompletePayment
    @PaymentId INT,
    @RazorpayPaymentId NVARCHAR(100),
    @RazorpaySignature NVARCHAR(500),
    @TransactionId NVARCHAR(200)
AS
BEGIN
    SET NOCOUNT ON;
    
    DECLARE @Metadata NVARCHAR(MAX);
    SELECT @Metadata = ISNULL(Metadata, '{}') FROM Payments WHERE Id = @PaymentId;
    
    -- Update metadata with transaction ID
    SET @Metadata = REPLACE(@Metadata, '}', ', "transactionId": "' + @TransactionId + '"}');
    
    -- Update payment status
    UPDATE Payments 
    SET 
        Status = 'Completed',
        RazorpayPaymentId = @RazorpayPaymentId,
        RazorpaySignature = @RazorpaySignature,
        Metadata = @Metadata,
        UpdatedAt = GETUTCDATE()
    WHERE Id = @PaymentId;
    
    -- Get associated license ID from payment reference
    DECLARE @LicenseId INT;
    DECLARE @PaymentRef NVARCHAR(200);
    
    SELECT @PaymentRef = PaymentReference FROM Payments WHERE Id = @PaymentId;
    
    -- Extract license ID from reference (format: LIC_123)
    IF @PaymentRef LIKE 'LIC_%'
    BEGIN
        SET @LicenseId = CAST(SUBSTRING(@PaymentRef, 5, LEN(@PaymentRef)) AS INT);
        
        -- Update license status to Active (automatic activation after payment)
        UPDATE Licenses 
        SET Status = 'Active', UpdatedAt = GETUTCDATE()
        WHERE Id = @LicenseId;
        
        -- Add history entry
        INSERT INTO LicenseHistory (LicenseId, Timestamp, Action, PerformedBy, Details)
        SELECT 
            @LicenseId,
            GETUTCDATE(),
            'Payment Completed - License Activated',
            l.ApplicantEmail,
            'Payment completed with transaction ID: ' + @TransactionId + '. License automatically activated.'
        FROM Licenses l
        WHERE l.Id = @LicenseId;
    END
END
GO

-- =====================================================
-- STORED PROCEDURE: sp_GetPaymentByLicenseId
-- =====================================================
CREATE OR ALTER PROCEDURE sp_GetPaymentByLicenseId
    @LicenseId INT
AS
BEGIN
    SET NOCOUNT ON;
    
    SELECT 
        p.Id,
        @LicenseId as LicenseId,
        p.Amount,
        JSON_VALUE(p.Metadata, '$.paymentMethod') as PaymentMethod,
        p.Status as PaymentStatus,
        JSON_VALUE(p.Metadata, '$.transactionId') as TransactionId,
        p.InvoiceId,
        p.RazorpayOrderId,
        p.RazorpayPaymentId,
        p.CreatedAt,
        p.UpdatedAt,
        l.ApplicantName,
        l.LicenseType,
        l.Status AS LicenseStatus
    FROM Licenses l
    LEFT JOIN Payments p ON l.PaymentId = p.Id
    WHERE l.Id = @LicenseId;
END
GO

-- =====================================================
-- NEW SECURITY-ENHANCED STORED PROCEDURES
-- =====================================================

-- Stored Procedure: Get License Data for Payment Creation
-- Retrieves license and user details for Razorpay invoice generation
CREATE OR ALTER PROCEDURE sp_GetLicenseDataForPayment
    @LicenseId INT,
    @TenantId NVARCHAR(50)
AS
BEGIN
    SET NOCOUNT ON;
    
    SELECT 
        l.Id, 
        l.ApplicantName, 
        l.LicenseType, 
        l.IssueDate, 
        l.ExpiryDate,
        u.Username, 
        u.Email, 
        t.Name as TenantName, 
        ISNULL(lt.TypeName, l.LicenseType) as TypeName, 
        ISNULL(lt.DurationMonths, 12) as DurationMonths
    FROM Licenses l
    INNER JOIN Users u ON l.UserId = u.Id
    INNER JOIN Tenants t ON l.TenantId = t.Id
    LEFT JOIN LicenseTypes lt ON l.LicenseType = lt.TypeName
    WHERE l.Id = @LicenseId 
    AND l.TenantId = @TenantId;
END
GO

-- Stored Procedure: Update Payment Razorpay Signature
-- Updates payment record with Razorpay invoice ID in signature field
CREATE OR ALTER PROCEDURE sp_UpdatePaymentRazorpaySignature
    @PaymentId INT,
    @RazorpayInvoiceId NVARCHAR(255)
AS
BEGIN
    SET NOCOUNT ON;
    
    UPDATE Payments 
    SET RazorpaySignature = @RazorpayInvoiceId,
        UpdatedAt = GETUTCDATE()
    WHERE Id = @PaymentId;
    
    SELECT @@ROWCOUNT AS RowsAffected;
END
GO

-- Stored Procedure: Get Payment with License Details for Invoice
-- Retrieves payment and associated license details for invoice generation
CREATE OR ALTER PROCEDURE sp_GetPaymentWithLicenseDetails
    @PaymentId INT,
    @TenantId NVARCHAR(50) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    
    DECLARE @sql NVARCHAR(MAX);
    
    SET @sql = '
    SELECT 
        p.Id,
        p.PaymentReference,
        p.Amount,
        p.Currency,
        p.Status,
        p.PaymentMethod,
        p.RazorpayOrderId,
        p.RazorpayPaymentId,
        p.RazorpaySignature,
        p.InvoiceId,
        p.CreatedAt,
        p.UpdatedAt,
        p.TenantId,
        l.ApplicantName,
        l.ApplicantEmail,
        l.LicenseType,
        l.CreatedAt as IssueDate,
        l.ExpiryDate,
        l.LicenseNumber
    FROM Payments p
    LEFT JOIN Licenses l ON p.LicenseId = l.Id
    WHERE p.Id = @PaymentId';
    
    -- Add tenant filter if provided
    IF @TenantId IS NOT NULL
    BEGIN
        SET @sql = @sql + ' AND p.TenantId = @TenantId';
    END
    
    EXEC sp_executesql @sql, N'@PaymentId INT, @TenantId NVARCHAR(50)', @PaymentId, @TenantId;
END
GO

-- Stored Procedure: Link Payment to License
-- Updates payment and license records to establish relationship
CREATE OR ALTER PROCEDURE sp_LinkPaymentToLicense
    @PaymentId INT,
    @LicenseId INT,
    @TenantId NVARCHAR(50)
AS
BEGIN
    SET NOCOUNT ON;
    
    BEGIN TRANSACTION;
    
    BEGIN TRY
        -- Validate payment exists and belongs to tenant
        IF NOT EXISTS (SELECT 1 FROM Payments WHERE Id = @PaymentId AND TenantId = @TenantId)
        BEGIN
            ROLLBACK TRANSACTION;
            THROW 50001, 'Payment not found or access denied', 1;
        END
        
        -- Validate license exists and belongs to tenant
        IF NOT EXISTS (SELECT 1 FROM Licenses WHERE Id = @LicenseId AND TenantId = @TenantId)
        BEGIN
            ROLLBACK TRANSACTION;
            THROW 50002, 'License not found or access denied', 1;
        END
        
        -- Update payment reference to use the actual license ID
        UPDATE Payments 
        SET PaymentReference = 'LIC_' + CAST(@LicenseId AS NVARCHAR(50)),
            LicenseId = @LicenseId,
            UpdatedAt = GETUTCDATE() 
        WHERE Id = @PaymentId;
        
        -- Update license with payment ID
        UPDATE Licenses 
        SET PaymentId = @PaymentId,
            UpdatedAt = GETUTCDATE() 
        WHERE Id = @LicenseId;
        
        COMMIT TRANSACTION;
        
        SELECT 
            @PaymentId as PaymentId,
            @LicenseId as LicenseId,
            'SUCCESS' as Status,
            'Payment linked to license successfully' as Message;
            
    END TRY
    BEGIN CATCH
        ROLLBACK TRANSACTION;
        
        DECLARE @ErrorMessage NVARCHAR(4000) = ERROR_MESSAGE();
        DECLARE @ErrorSeverity INT = ERROR_SEVERITY();
        DECLARE @ErrorState INT = ERROR_STATE();
        
        RAISERROR(@ErrorMessage, @ErrorSeverity, @ErrorState);
    END CATCH
END
GO

-- Stored Procedure: Get Active License Types
-- Retrieves all active license types with their details
CREATE OR ALTER PROCEDURE sp_GetActiveLicenseTypes
AS
BEGIN
    SET NOCOUNT ON;
    
    SELECT 
        Id, 
        TypeName, 
        Description, 
        Amount, 
        DurationMonths, 
        IsActive, 
        CreatedAt, 
        UpdatedAt 
    FROM LicenseTypes 
    WHERE IsActive = 1 
    ORDER BY TypeName;
END
GO

-- Stored Procedure: Get Tenant Users
-- Retrieves all active users for a specific tenant
CREATE OR ALTER PROCEDURE sp_GetTenantUsers
    @TenantId NVARCHAR(50)
AS
BEGIN
    SET NOCOUNT ON;
    
    -- Validate tenant exists
    IF NOT EXISTS (SELECT 1 FROM Tenants WHERE TenantId = @TenantId)
    BEGIN
        THROW 50001, 'Invalid TenantId', 1;
    END
    
    SELECT 
        Id, 
        Username, 
        Email, 
        Roles, 
        TenantId, 
        IsActive,
        CreatedAt,
        UpdatedAt
    FROM Users 
    WHERE TenantId = @TenantId 
    AND IsActive = 1 
    ORDER BY Username;
END
GO

-- Stored Procedure: Delete License
-- Safely deletes a license and its associated records
CREATE OR ALTER PROCEDURE sp_DeleteLicense
    @LicenseId INT,
    @TenantId NVARCHAR(50)
AS
BEGIN
    SET NOCOUNT ON;
    
    BEGIN TRANSACTION;
    
    BEGIN TRY
        -- Validate license exists and belongs to tenant
        IF NOT EXISTS (SELECT 1 FROM Licenses WHERE Id = @LicenseId AND TenantId = @TenantId)
        BEGIN
            ROLLBACK TRANSACTION;
            THROW 50001, 'License not found or access denied', 1;
        END
        
        -- Delete associated records first (to maintain referential integrity)
        DELETE FROM LicenseHistory 
        WHERE LicenseId = @LicenseId;
        
        -- Delete associated documents
        DELETE FROM Documents 
        WHERE LicenseId = @LicenseId;
        
        -- Delete the license
        DELETE FROM Licenses 
        WHERE Id = @LicenseId 
        AND TenantId = @TenantId;
        
        COMMIT TRANSACTION;
        
        SELECT 
            @LicenseId as LicenseId,
            'SUCCESS' as Status,
            'License and associated records deleted successfully' as Message;
            
    END TRY
    BEGIN CATCH
        ROLLBACK TRANSACTION;
        
        DECLARE @ErrorMessage NVARCHAR(4000) = ERROR_MESSAGE();
        DECLARE @ErrorSeverity INT = ERROR_SEVERITY();
        DECLARE @ErrorState INT = ERROR_STATE();
        
        RAISERROR(@ErrorMessage, @ErrorSeverity, @ErrorState);
    END CATCH
END
GO

-- Stored Procedure: Check License Exists for Tenant
-- Verifies if a license exists and belongs to the specified tenant
CREATE OR ALTER PROCEDURE sp_CheckLicenseExists
    @LicenseId INT,
    @TenantId NVARCHAR(50)
AS
BEGIN
    SET NOCOUNT ON;
    
    SELECT CAST(CASE WHEN EXISTS (
        SELECT 1 FROM Licenses 
        WHERE Id = @LicenseId 
        AND TenantId = @TenantId
    ) THEN 1 ELSE 0 END AS BIT) as LicenseExists;
END
GO

-- Stored Procedure: Update License Status
-- Updates the status of a license
CREATE OR ALTER PROCEDURE sp_UpdateLicenseStatus
    @LicenseId INT,
    @Status NVARCHAR(50),
    @TenantId NVARCHAR(50) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    
    DECLARE @sql NVARCHAR(MAX) = 'UPDATE Licenses SET Status = @Status, UpdatedAt = GETUTCDATE() WHERE Id = @LicenseId';
    
    -- Add tenant filter if provided for additional security
    IF @TenantId IS NOT NULL
    BEGIN
        SET @sql = @sql + ' AND TenantId = @TenantId';
    END
    
    EXEC sp_executesql @sql, N'@LicenseId INT, @Status NVARCHAR(50), @TenantId NVARCHAR(50)', @LicenseId, @Status, @TenantId;
    
    SELECT @@ROWCOUNT AS RowsAffected;
END
GO

-- =====================================================
-- INDEXES for Performance
-- =====================================================

-- Users indexes
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_Users_Email')
    CREATE INDEX IX_Users_Email ON Users(Email);

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_Users_TenantId')
    CREATE INDEX IX_Users_TenantId ON Users(TenantId);

-- Licenses indexes
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_Licenses_LicenseNumber')
    CREATE INDEX IX_Licenses_LicenseNumber ON Licenses(LicenseNumber);

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_Licenses_TenantId')
    CREATE INDEX IX_Licenses_TenantId ON Licenses(TenantId);

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_Licenses_Status')
    CREATE INDEX IX_Licenses_Status ON Licenses(Status);

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_Licenses_UserId')
    CREATE INDEX IX_Licenses_UserId ON Licenses(UserId);

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_Licenses_PaymentId')
    CREATE INDEX IX_Licenses_PaymentId ON Licenses(PaymentId);

-- Payments indexes
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_Payments_PaymentReference')
    CREATE INDEX IX_Payments_PaymentReference ON Payments(PaymentReference);

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_Payments_TenantId')
    CREATE INDEX IX_Payments_TenantId ON Payments(TenantId);

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_Payments_Status')
    CREATE INDEX IX_Payments_Status ON Payments(Status);

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_Payments_RazorpayOrderId')
    CREATE INDEX IX_Payments_RazorpayOrderId ON Payments(RazorpayOrderId);

-- Documents indexes
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_Documents_LicenseId')
    CREATE INDEX IX_Documents_LicenseId ON Documents(LicenseId);

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_Documents_TenantId')
    CREATE INDEX IX_Documents_TenantId ON Documents(TenantId);

-- Notifications indexes
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_Notifications_UserId')
    CREATE INDEX IX_Notifications_UserId ON Notifications(UserId);

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_Notifications_IsRead')
    CREATE INDEX IX_Notifications_IsRead ON Notifications(IsRead);

-- LicenseHistory indexes
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_LicenseHistory_LicenseId')
    CREATE INDEX IX_LicenseHistory_LicenseId ON LicenseHistory(LicenseId);

PRINT 'All indexes created successfully.';
GO

-- =====================================================
-- SAMPLE DATA - Admin User
-- =====================================================
IF NOT EXISTS (SELECT * FROM Users WHERE Email = 'admin@gov2biz.com')
BEGIN
    INSERT INTO Users (Username, Email, PasswordHash, Roles, TenantId, IsActive)
    VALUES (
        'admin',
        'admin@gov2biz.com',
        'AQAAAAIAAYagAAAAEKxZ8FbZHvK5LZlL8HxN9wX1Z2QG9HvK5LZlL8HxN9wX1Z2QG9H', -- Hash for: Admin@123
        'Admin',
        'SYSTEM',
        1
    );
    PRINT 'Admin user created successfully.';
END
GO

-- =====================================================
-- SAMPLE DATA - Test Users
-- =====================================================
IF NOT EXISTS (SELECT * FROM Users WHERE Email = 'audit@test1.com')
BEGIN
    INSERT INTO Users (Username, Email, PasswordHash, Roles, TenantId, IsActive)
    VALUES 
        ('audit_user', 'audit@test1.com', 'AQAAAAIAAYagAAAAEKxZ8FbZHvK5LZlL8HxN9wX1Z2QG9HvK5LZlL8HxN9wX1Z2QG9H', 'User', 'AUDIT-TENANT', 1),
        ('finance_user', 'finance@test1.com', 'AQAAAAIAAYagAAAAEKxZ8FbZHvK5LZlL8HxN9wX1Z2QG9HvK5LZlL8HxN9wX1Z2QG9H', 'User', 'FINANCE-TENANT', 1),
        ('hr_user', 'hr@test1.com', 'AQAAAAIAAYagAAAAEKxZ8FbZHvK5LZlL8HxN9wX1Z2QG9HvK5LZlL8HxN9wX1Z2QG9H', 'User', 'HR-TENANT', 1);
    PRINT 'Test users created successfully.';
END
GO

-- =====================================================
-- COMPLETION MESSAGE
-- =====================================================
PRINT '';
PRINT '===========================================';
PRINT 'Gov2Biz Database Setup Complete!';
PRINT '===========================================';
PRINT 'Database: Gov2Biz';
PRINT 'Tables Created: 6';
PRINT 'Stored Procedures: 12';
PRINT 'Indexes: 15+';
PRINT '';
PRINT 'Default Admin Credentials:';
PRINT 'Email: admin@gov2biz.com';
PRINT 'Password: Admin@123';
PRINT '';
PRINT 'Test User Credentials:';
PRINT 'Email: audit@test1.com';
PRINT 'Password: Test@123';
PRINT '===========================================';
GO

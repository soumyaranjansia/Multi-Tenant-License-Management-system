-- =====================================================
-- Gov2Biz Database Complete Dump - MAIN FILE
-- Generated on: 2025-11-02
-- Purpose: Complete database schema and data backup
-- Includes: Tables, Data, Stored Procedures, Indexes, Constraints
-- =====================================================

USE master;
GO

-- Drop and recreate database
IF EXISTS (SELECT name FROM sys.databases WHERE name = N'Gov2Biz')
BEGIN
    ALTER DATABASE Gov2Biz SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
    DROP DATABASE Gov2Biz;
END
GO

CREATE DATABASE Gov2Biz;
GO

USE Gov2Biz;
GO

PRINT '=== Gov2Biz Database Schema and Data Dump ===';
PRINT 'Generated: 2025-11-02';
PRINT 'Status: JWT Authorization Secured + License Management Complete';
GO

-- =====================================================
-- TABLE CREATION SECTION
-- =====================================================

-- 1. Tenants Table
CREATE TABLE Tenants (
    TenantId NVARCHAR(50) PRIMARY KEY,
    Name NVARCHAR(256) NOT NULL,
    CreatedAt DATETIME2 DEFAULT SYSUTCDATETIME(),
    IsActive BIT DEFAULT 1,
    Metadata NVARCHAR(MAX) NULL
);

-- 2. Users Table  
CREATE TABLE Users (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    Username NVARCHAR(256) NOT NULL,
    Email NVARCHAR(256) NOT NULL,
    PasswordHash NVARCHAR(512) NOT NULL,
    Roles NVARCHAR(512) NOT NULL,
    TenantId NVARCHAR(50) NOT NULL,
    CreatedAt DATETIME2 DEFAULT SYSUTCDATETIME(),
    IsActive BIT DEFAULT 1,
    UpdatedAt DATETIME2 DEFAULT SYSUTCDATETIME(),
    LastLoginAt DATETIME2 NULL,
    FOREIGN KEY (TenantId) REFERENCES Tenants(TenantId),
    UNIQUE(Email, TenantId),
    UNIQUE(Username, TenantId)
);

-- 3. LicenseTypes Table
CREATE TABLE LicenseTypes (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    Name NVARCHAR(100) NOT NULL UNIQUE,
    Description NVARCHAR(500) NULL,
    Price DECIMAL(18,2) NOT NULL DEFAULT 0.00,
    ValidityMonths INT NOT NULL DEFAULT 12,
    IsActive BIT DEFAULT 1,
    CreatedAt DATETIME2 DEFAULT SYSUTCDATETIME(),
    UpdatedAt DATETIME2 DEFAULT SYSUTCDATETIME()
);

-- 4. Licenses Table (with IsAdminCreated field)
CREATE TABLE Licenses (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    LicenseNumber NVARCHAR(100) NOT NULL UNIQUE,
    ApplicantName NVARCHAR(256) NOT NULL,
    ApplicantEmail NVARCHAR(256) NOT NULL,
    LicenseType NVARCHAR(100) NOT NULL,
    Status NVARCHAR(50) NOT NULL DEFAULT 'Pending', -- Pending, Active, Expired, Suspended, Renewed, Rejected
    ExpiryDate DATETIME2 NOT NULL,
    Metadata NVARCHAR(MAX) NULL,
    TenantId NVARCHAR(50) NOT NULL,
    CreatedAt DATETIME2 DEFAULT SYSUTCDATETIME(),
    UpdatedAt DATETIME2 DEFAULT SYSUTCDATETIME(),
    Amount DECIMAL(18,2) NOT NULL DEFAULT 0.00,
    PaymentId INT NULL,
    UserId INT NULL,
    IssueDate DATETIME2 NULL,
    IsAdminCreated BIT NOT NULL DEFAULT 0, -- Track admin vs user created licenses
    FOREIGN KEY (TenantId) REFERENCES Tenants(TenantId),
    FOREIGN KEY (UserId) REFERENCES Users(Id)
);

-- 5. Payments Table
CREATE TABLE Payments (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    PaymentReference NVARCHAR(200) NOT NULL,
    Amount DECIMAL(18,2) NOT NULL,
    Status NVARCHAR(50) NOT NULL DEFAULT 'Pending', -- Pending, Completed, Failed, Cancelled
    PaymentMethod NVARCHAR(100) NOT NULL DEFAULT 'razorpay',
    TenantId NVARCHAR(50) NOT NULL,
    CreatedAt DATETIME2 DEFAULT SYSUTCDATETIME(),
    UpdatedAt DATETIME2 DEFAULT SYSUTCDATETIME(),
    RazorpayOrderId NVARCHAR(200) NULL,
    RazorpayPaymentId NVARCHAR(200) NULL,
    RazorpaySignature NVARCHAR(500) NULL,
    InvoiceId NVARCHAR(100) NULL,
    Metadata NVARCHAR(MAX) NULL,
    LicenseId INT NULL,
    FOREIGN KEY (TenantId) REFERENCES Tenants(TenantId),
    FOREIGN KEY (LicenseId) REFERENCES Licenses(Id)
);

-- 6. LicenseHistory Table
CREATE TABLE LicenseHistory (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    LicenseId INT NOT NULL,
    Timestamp DATETIME2 DEFAULT SYSUTCDATETIME(),
    Action NVARCHAR(200) NOT NULL, -- Created, Renewed, Suspended, Expired, etc.
    PerformedBy NVARCHAR(256) NOT NULL,
    Details NVARCHAR(1000) NULL,
    FOREIGN KEY (LicenseId) REFERENCES Licenses(Id)
);

-- Add indexes for performance
CREATE INDEX IX_Licenses_TenantId ON Licenses(TenantId);
CREATE INDEX IX_Licenses_Status ON Licenses(Status);
CREATE INDEX IX_Licenses_ExpiryDate ON Licenses(ExpiryDate);
CREATE INDEX IX_Licenses_ApplicantEmail ON Licenses(ApplicantEmail);
CREATE INDEX IX_Payments_TenantId ON Payments(TenantId);
CREATE INDEX IX_Payments_Status ON Payments(Status);
CREATE INDEX IX_LicenseHistory_LicenseId ON LicenseHistory(LicenseId);

-- Create Performance Indexes
CREATE INDEX IX_Licenses_TenantId ON Licenses(TenantId);
CREATE INDEX IX_Licenses_Status ON Licenses(Status);
CREATE INDEX IX_Licenses_ExpiryDate ON Licenses(ExpiryDate);
CREATE INDEX IX_Licenses_ApplicantEmail ON Licenses(ApplicantEmail);
CREATE INDEX IX_Licenses_IsAdminCreated ON Licenses(IsAdminCreated);
CREATE INDEX IX_Payments_TenantId ON Payments(TenantId);
CREATE INDEX IX_Payments_Status ON Payments(Status);
CREATE INDEX IX_LicenseHistory_LicenseId ON LicenseHistory(LicenseId);
CREATE INDEX IX_Users_Email ON Users(Email);
CREATE INDEX IX_Users_TenantId ON Users(TenantId);

PRINT 'Database schema created with indexes';
GO

-- =====================================================
-- SECTION 2: SEED DATA INSERTION  
-- =====================================================

-- Insert Tenants
INSERT INTO Tenants (TenantId, Name, CreatedAt, IsActive) VALUES
('1', 'TechCorp Solutions', '2025-11-01 18:47:26', 1),
('AUDIT-TENANT', 'Audit Company', '2025-11-01 18:47:26', 1),
('NEW-TENANT-123', 'New Business Ventures', '2025-11-01 18:47:26', 1),
('tenant_local', 'Local Business Group', '2025-11-01 18:47:26', 1),
('TEN-123456', 'Enterprise Solutions', '2025-11-01 18:47:26', 1);

-- Insert Users (Use actual bcrypt hashes in production)
INSERT INTO Users (TenantId, Username, Email, PasswordHash, Roles, IsActive, CreatedAt, UpdatedAt) VALUES 
('tenant1', 'admin1', 'admin1@tenant1.com', 'hashedpass1', 'Admin', 1, GETUTCDATE(), GETUTCDATE()),
('tenant2', 'user1', 'user1@tenant2.com', 'hashedpass2', 'User', 1, GETUTCDATE(), GETUTCDATE()),
('tenant3', 'admin2', 'admin2@tenant3.com', 'hashedpass3', 'Admin', 1, GETUTCDATE(), GETUTCDATE()),
('tenant4', 'user2', 'user2@tenant4.com', 'hashedpass4', 'User', 1, GETUTCDATE(), GETUTCDATE()),
('tenant5', 'auditor1', 'auditor1@tenant5.com', 'hashedpass5', 'Auditor', 1, GETUTCDATE(), GETUTCDATE());

-- Insert License Types
INSERT INTO LicenseTypes (Name, Description, Price, ValidityMonths, IsActive, CreatedAt, UpdatedAt) VALUES
('Business License', 'Standard business operating license', 150.00, 12, 1, GETUTCDATE(), GETUTCDATE()),
('Professional License', 'Professional service provider license', 200.00, 24, 1, GETUTCDATE(), GETUTCDATE()),
('Trade License', 'Import/Export trade license', 300.00, 36, 1, GETUTCDATE(), GETUTCDATE()),
('Retail License', 'Retail establishment license', 100.00, 12, 1, GETUTCDATE(), GETUTCDATE()),
('Manufacturing License', 'Manufacturing facility license', 500.00, 60, 1, GETUTCDATE(), GETUTCDATE()),
('Service License', 'Service industry license', 120.00, 18, 1, GETUTCDATE(), GETUTCDATE()),
('Healthcare License', 'Healthcare provider license', 400.00, 24, 1, GETUTCDATE(), GETUTCDATE()),
('Food Service License', 'Restaurant and food service license', 180.00, 12, 1, GETUTCDATE(), GETUTCDATE()),
('Transport License', 'Transportation service license', 250.00, 36, 1, GETUTCDATE(), GETUTCDATE()),
('Technology License', 'IT and technology service license', 220.00, 24, 1, GETUTCDATE(), GETUTCDATE());

-- =====================================================  
-- SECTION 3: STORED PROCEDURES
-- =====================================================

-- 1. License Creation Procedure (Enhanced with IsAdminCreated)
CREATE OR ALTER PROCEDURE sp_CreateLicense
    @ApplicantName NVARCHAR(256),
    @ApplicantEmail NVARCHAR(256),
    @LicenseType NVARCHAR(100),
    @ExpiryDate DATETIME2,
    @TenantId NVARCHAR(50),
    @Metadata NVARCHAR(MAX) = NULL,
    @Amount DECIMAL(18,2) = 100.00,
    @IsAdminCreated BIT = 0
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    BEGIN TRY
        BEGIN TRANSACTION;

        -- Validate tenant exists
        IF NOT EXISTS (SELECT 1 FROM Tenants WHERE TenantId = @TenantId AND IsActive = 1)
        BEGIN
            RAISERROR('Invalid or inactive tenant', 16, 1);
            RETURN;
        END

        DECLARE @NewId INT;
        DECLARE @LicenseNumber NVARCHAR(100);
        DECLARE @Year NVARCHAR(4) = FORMAT(GETUTCDATE(), 'yyyy');
        
        -- Generate unique license number: LIC-YYYY-NNNNNN
        DECLARE @NextSeq INT = ISNULL((SELECT MAX(Id) FROM Licenses), 0) + 1;
        SET @LicenseNumber = CONCAT('LIC-', @Year, '-', RIGHT('000000' + CAST(@NextSeq AS NVARCHAR(6)), 6));

        -- Insert license
        INSERT INTO Licenses (
            LicenseNumber, ApplicantName, ApplicantEmail, LicenseType, 
            Status, ExpiryDate, Amount, Metadata, TenantId, IsAdminCreated,
            CreatedAt, UpdatedAt
        )
        VALUES (
            @LicenseNumber, @ApplicantName, @ApplicantEmail, @LicenseType,
            'Pending', @ExpiryDate, @Amount, @Metadata, @TenantId, @IsAdminCreated,
            SYSUTCDATETIME(), SYSUTCDATETIME()
        );

        SET @NewId = SCOPE_IDENTITY();

        -- Add history entry
        INSERT INTO LicenseHistory (LicenseId, Action, PerformedBy, Details)
        VALUES (@NewId, 'Created', @ApplicantEmail, CONCAT('License created for ', @ApplicantName));

        COMMIT TRANSACTION;

        -- Return new ID and license number
        SELECT @NewId AS NewId, @LicenseNumber AS LicenseNumber;

    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0
            ROLLBACK TRANSACTION;
        
        DECLARE @ErrorMessage NVARCHAR(4000) = ERROR_MESSAGE();
        DECLARE @ErrorSeverity INT = ERROR_SEVERITY();
        DECLARE @ErrorState INT = ERROR_STATE();
        
        RAISERROR(@ErrorMessage, @ErrorSeverity, @ErrorState);
    END CATCH
END
GO

-- 2. Payment Linking Procedure (Enhanced with Admin License Auto-Activation)
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
        
        -- Check if payment is completed and if license was created by admin
        DECLARE @PaymentStatus NVARCHAR(50);
        DECLARE @ApplicantEmail NVARCHAR(256);
        DECLARE @IsAdminCreated BIT;
        
        SELECT @PaymentStatus = Status 
        FROM Payments 
        WHERE Id = @PaymentId;
        
        SELECT @ApplicantEmail = ApplicantEmail, @IsAdminCreated = IsAdminCreated 
        FROM Licenses 
        WHERE Id = @LicenseId;
        
        -- If payment is completed, only activate admin-created licenses automatically
        IF @PaymentStatus = 'Completed'
        BEGIN
            IF @IsAdminCreated = 1
            BEGIN
                -- Admin-created license: Auto-activate after payment
                UPDATE Licenses 
                SET Status = 'Active', UpdatedAt = GETUTCDATE()
                WHERE Id = @LicenseId;
                
                INSERT INTO LicenseHistory (LicenseId, Timestamp, Action, PerformedBy, Details)
                VALUES (
                    @LicenseId, GETUTCDATE(), 'Payment Linked - Admin License Activated',
                    @ApplicantEmail, 'Payment ' + CAST(@PaymentId AS NVARCHAR(10)) + ' linked and admin-created license automatically activated.'
                );
                
                PRINT 'Admin-created license ' + CAST(@LicenseId AS NVARCHAR(10)) + ' activated after payment';
            END
            ELSE
            BEGIN
                -- User-created license: Keep as Pending, requires admin approval
                INSERT INTO LicenseHistory (LicenseId, Timestamp, Action, PerformedBy, Details)
                VALUES (
                    @LicenseId, GETUTCDATE(), 'Payment Linked - Awaiting Approval',
                    @ApplicantEmail, 'Payment ' + CAST(@PaymentId AS NVARCHAR(10)) + ' linked successfully. User-created license remains Pending awaiting admin approval.'
                );
                
                PRINT 'User-created license ' + CAST(@LicenseId AS NVARCHAR(10)) + ' remains Pending (awaiting admin approval)';
            END
        END
        
        COMMIT TRANSACTION;
        
        SELECT 
            @PaymentId as PaymentId,
            @LicenseId as LicenseId,
            'SUCCESS' as Status,
            CASE 
                WHEN @PaymentStatus = 'Completed' AND @IsAdminCreated = 1 THEN 'Payment linked and admin license activated successfully'
                WHEN @PaymentStatus = 'Completed' AND @IsAdminCreated = 0 THEN 'Payment linked successfully. User license remains pending admin approval.'
                ELSE 'Payment linked to license successfully'
            END as Message;
            
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0
            ROLLBACK TRANSACTION;
        THROW;
    END CATCH
END
GO

-- 3. Payment Completion Procedure
CREATE OR ALTER PROCEDURE sp_CompletePayment
    @PaymentId INT,
    @RazorpayPaymentId NVARCHAR(100),
    @RazorpaySignature NVARCHAR(500),
    @TransactionId NVARCHAR(200)
AS
BEGIN
    SET NOCOUNT ON;
    
    DECLARE @Metadata NVARCHAR(MAX);
    DECLARE @LicenseId INT;
    
    -- Get payment details and associated license
    SELECT @Metadata = ISNULL(Metadata, '{}'), @LicenseId = LicenseId 
    FROM Payments 
    WHERE Id = @PaymentId;
    
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
    
    -- License activation is handled by sp_LinkPaymentToLicense
    PRINT 'Payment ' + CAST(@PaymentId AS NVARCHAR(10)) + ' completed. License activation handled by linking procedure.';
END
GO

-- 4. License Status Update Procedure  
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

-- 5. Get Licenses by Tenant
CREATE OR ALTER PROCEDURE sp_GetLicensesByTenant
    @TenantId NVARCHAR(50),
    @Status NVARCHAR(50) = NULL,
    @ExpiringInDays INT = NULL
AS
BEGIN
    SET NOCOUNT ON;
    
    SELECT 
        Id, LicenseNumber, ApplicantName, ApplicantEmail,
        LicenseType, Status, ExpiryDate, Amount,
        CreatedAt, UpdatedAt, IsAdminCreated
    FROM Licenses
    WHERE TenantId = @TenantId
    AND (@Status IS NULL OR Status = @Status)
    AND (@ExpiringInDays IS NULL OR ExpiryDate <= DATEADD(DAY, @ExpiringInDays, GETUTCDATE()))
    ORDER BY CreatedAt DESC;
END
GO

-- 6. Get Active License Types
CREATE OR ALTER PROCEDURE sp_GetActiveLicenseTypes
AS
BEGIN
    SET NOCOUNT ON;
    
    SELECT Id, Name, Description, Price, ValidityMonths, IsActive
    FROM LicenseTypes
    WHERE IsActive = 1
    ORDER BY Name;
END
GO

-- 7. Get Tenant Users (Admin functionality)
CREATE OR ALTER PROCEDURE sp_GetTenantUsers
    @TenantId NVARCHAR(50)
AS
BEGIN
    SET NOCOUNT ON;
    
    SELECT Id, Username, Email, Roles, IsActive, CreatedAt
    FROM Users
    WHERE TenantId = @TenantId AND IsActive = 1
    ORDER BY Username;
END
GO

-- Success message
PRINT 'Gov2Biz Main Database Dump created successfully with schema, indexes, seed data and stored procedures';
PRINT 'Database is ready for deployment';
PRINT 'All stored procedures created successfully';
GO

-- Insert License Types with Pricing
INSERT INTO LicenseTypes (Name, Description, Price, ValidityMonths, IsActive) VALUES
('Business', 'General Business License', 300.00, 12, 1),
('Professional', 'Professional Service License', 450.00, 12, 1),
('Trade', 'Trade and Commerce License', 350.00, 12, 1),
('Construction', 'Construction and Building License', 550.00, 12, 1),
('Food Service', 'Food Service and Restaurant License', 400.00, 12, 1),
('Retail', 'Retail and Sales License', 250.00, 12, 1),
('Healthcare', 'Healthcare Service License', 600.00, 12, 1),
('Education', 'Educational Institution License', 500.00, 12, 1),
('Transportation', 'Transportation Service License', 450.00, 12, 1),
('Real Estate', 'Real Estate Service License', 400.00, 12, 1);

PRINT 'Seed data inserted successfully';
GO
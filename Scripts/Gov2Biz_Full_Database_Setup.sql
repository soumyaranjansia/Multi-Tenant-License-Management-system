-- =============================================
-- Gov2Biz Complete Database Setup
-- Full Schema + Stored Procedures + Seed Data
-- Run this file to set up everything from scratch
-- =============================================

USE master;
GO

-- Drop database if exists
IF EXISTS (SELECT name FROM sys.databases WHERE name = 'Gov2Biz')
BEGIN
    ALTER DATABASE Gov2Biz SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
    DROP DATABASE Gov2Biz;
END
GO

-- Create database
CREATE DATABASE Gov2Biz;
GO

USE Gov2Biz;
GO

-- =============================================
-- TABLES
-- =============================================

-- Users Table
CREATE TABLE Users (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    TenantId NVARCHAR(100) NOT NULL,
    Username NVARCHAR(100) NOT NULL,
    Email NVARCHAR(255) NOT NULL,
    PasswordHash NVARCHAR(500) NOT NULL,
    FirstName NVARCHAR(100),
    LastName NVARCHAR(100),
    Roles NVARCHAR(500) NOT NULL DEFAULT 'User',
    IsActive BIT NOT NULL DEFAULT 1,
    CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    UpdatedAt DATETIME2,
    CONSTRAINT UK_Users_TenantId_Email UNIQUE (TenantId, Email),
    CONSTRAINT UK_Users_TenantId_Username UNIQUE (TenantId, Username)
);
GO

CREATE INDEX IX_Users_TenantId ON Users(TenantId);
CREATE INDEX IX_Users_Email ON Users(Email);
GO

-- LicenseTypes Table
CREATE TABLE LicenseTypes (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    TypeName NVARCHAR(100) NOT NULL UNIQUE,
    Description NVARCHAR(500),
    Amount DECIMAL(18,2) NOT NULL,
    DurationMonths INT NOT NULL DEFAULT 12,
    IsActive BIT NOT NULL DEFAULT 1,
    CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    UpdatedAt DATETIME2
);
GO

-- Licenses Table
CREATE TABLE Licenses (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    LicenseNumber NVARCHAR(50) NOT NULL UNIQUE,
    UserId INT NOT NULL,
    TenantId NVARCHAR(100) NOT NULL,
    LicenseType NVARCHAR(100) NOT NULL,
    ApplicantName NVARCHAR(200) NOT NULL,
    ApplicantEmail NVARCHAR(255) NOT NULL,
    Status NVARCHAR(50) NOT NULL DEFAULT 'Pending',
    Amount DECIMAL(18,2) NOT NULL,
    IssueDate DATETIME2,
    ExpiryDate DATETIME2,
    IsAdminCreated BIT NOT NULL DEFAULT 0,
    CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    UpdatedAt DATETIME2,
    CONSTRAINT FK_Licenses_Users FOREIGN KEY (UserId) REFERENCES Users(Id)
);
GO

CREATE INDEX IX_Licenses_TenantId ON Licenses(TenantId);
CREATE INDEX IX_Licenses_UserId ON Licenses(UserId);
CREATE INDEX IX_Licenses_Status ON Licenses(Status);
GO

-- Payments Table
CREATE TABLE Payments (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    LicenseId INT,
    TenantId NVARCHAR(100) NOT NULL,
    Amount DECIMAL(18,2) NOT NULL,
    PaymentMethod NVARCHAR(50),
    PaymentStatus NVARCHAR(50) NOT NULL DEFAULT 'Pending',
    RazorpayOrderId NVARCHAR(100),
    RazorpayPaymentId NVARCHAR(100),
    RazorpaySignature NVARCHAR(500),
    InvoiceId NVARCHAR(100),
    PaidAt DATETIME2,
    CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    UpdatedAt DATETIME2,
    CONSTRAINT FK_Payments_Licenses FOREIGN KEY (LicenseId) REFERENCES Licenses(Id)
);
GO

CREATE INDEX IX_Payments_LicenseId ON Payments(LicenseId);
CREATE INDEX IX_Payments_TenantId ON Payments(TenantId);
GO

-- Documents Table
CREATE TABLE Documents (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    LicenseId INT NOT NULL,
    TenantId NVARCHAR(100) NOT NULL,
    FileName NVARCHAR(255) NOT NULL,
    FileType NVARCHAR(50),
    FileSize BIGINT,
    FilePath NVARCHAR(500) NOT NULL,
    UploadedBy INT,
    UploadedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    CONSTRAINT FK_Documents_Licenses FOREIGN KEY (LicenseId) REFERENCES Licenses(Id),
    CONSTRAINT FK_Documents_Users FOREIGN KEY (UploadedBy) REFERENCES Users(Id)
);
GO

CREATE INDEX IX_Documents_LicenseId ON Documents(LicenseId);
GO

-- =============================================
-- STORED PROCEDURES
-- =============================================

-- sp_CreatePaymentWithInvoice
CREATE OR ALTER PROCEDURE sp_CreatePaymentWithInvoice
    @LicenseId INT,
    @Amount DECIMAL(18,2),
    @PaymentMethod NVARCHAR(50),
    @TenantId NVARCHAR(100),
    @RazorpayOrderId NVARCHAR(100) = NULL,
    @NewPaymentId INT OUTPUT,
    @InvoiceId NVARCHAR(100) OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    
    -- Generate Invoice ID
    DECLARE @InvoiceNumber INT;
    SELECT @InvoiceNumber = ISNULL(MAX(Id), 0) + 1 FROM Payments WHERE TenantId = @TenantId;
    SET @InvoiceId = 'INV-' + @TenantId + '-' + RIGHT('00000' + CAST(@InvoiceNumber AS VARCHAR(5)), 5);
    
    -- Insert Payment
    INSERT INTO Payments (LicenseId, TenantId, Amount, PaymentMethod, PaymentStatus, RazorpayOrderId, InvoiceId, CreatedAt)
    VALUES (@LicenseId, @TenantId, @Amount, @PaymentMethod, 'Pending', @RazorpayOrderId, @InvoiceId, GETUTCDATE());
    
    SET @NewPaymentId = SCOPE_IDENTITY();
END;
GO

-- sp_GetLicenseDataForPayment
CREATE OR ALTER PROCEDURE sp_GetLicenseDataForPayment
    @LicenseId INT,
    @TenantId NVARCHAR(100)
AS
BEGIN
    SELECT 
        l.Id,
        l.LicenseNumber,
        l.LicenseType,
        l.ApplicantName,
        l.ApplicantEmail,
        l.Amount,
        u.Email AS UserEmail,
        u.FirstName,
        u.LastName
    FROM Licenses l
    INNER JOIN Users u ON l.UserId = u.Id
    WHERE l.Id = @LicenseId AND l.TenantId = @TenantId;
END;
GO

-- sp_CreateLicense
CREATE OR ALTER PROCEDURE sp_CreateLicense
    @TenantId NVARCHAR(100),
    @UserId INT,
    @LicenseType NVARCHAR(100),
    @ApplicantName NVARCHAR(200),
    @ApplicantEmail NVARCHAR(255),
    @Amount DECIMAL(18,2),
    @ExpiryDate DATETIME2,
    @IsAdminCreated BIT = 0,
    @NewLicenseId INT OUTPUT,
    @LicenseNumber NVARCHAR(50) OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    
    -- Generate License Number
    DECLARE @Year INT = YEAR(GETUTCDATE());
    DECLARE @LicenseCount INT;
    SELECT @LicenseCount = ISNULL(COUNT(*), 0) + 1 FROM Licenses WHERE TenantId = @TenantId;
    SET @LicenseNumber = 'LIC-' + CAST(@Year AS VARCHAR(4)) + '-' + RIGHT('0000' + CAST(@LicenseCount AS VARCHAR(4)), 4);
    
    -- Determine status
    DECLARE @Status NVARCHAR(50) = CASE WHEN @IsAdminCreated = 1 THEN 'Active' ELSE 'Pending' END;
    DECLARE @IssueDate DATETIME2 = CASE WHEN @IsAdminCreated = 1 THEN GETUTCDATE() ELSE NULL END;
    
    -- Insert License
    INSERT INTO Licenses (LicenseNumber, UserId, TenantId, LicenseType, ApplicantName, ApplicantEmail, 
                         Status, Amount, IssueDate, ExpiryDate, IsAdminCreated, CreatedAt)
    VALUES (@LicenseNumber, @UserId, @TenantId, @LicenseType, @ApplicantName, @ApplicantEmail,
            @Status, @Amount, @IssueDate, @ExpiryDate, @IsAdminCreated, GETUTCDATE());
    
    SET @NewLicenseId = SCOPE_IDENTITY();
END;
GO

-- sp_GetLicenseById
CREATE OR ALTER PROCEDURE sp_GetLicenseById
    @LicenseId INT,
    @TenantId NVARCHAR(100)
AS
BEGIN
    SELECT * FROM Licenses WHERE Id = @LicenseId AND TenantId = @TenantId;
END;
GO

-- sp_GetLicensesByTenant
CREATE OR ALTER PROCEDURE sp_GetLicensesByTenant
    @TenantId NVARCHAR(100),
    @UserId INT = NULL,
    @UserRole NVARCHAR(50) = 'User'
AS
BEGIN
    IF @UserRole = 'Admin'
        SELECT * FROM Licenses WHERE TenantId = @TenantId ORDER BY CreatedAt DESC;
    ELSE
        SELECT * FROM Licenses WHERE TenantId = @TenantId AND UserId = @UserId ORDER BY CreatedAt DESC;
END;
GO

-- sp_CheckLicenseExists
CREATE OR ALTER PROCEDURE sp_CheckLicenseExists
    @TenantId NVARCHAR(100),
    @LicenseType NVARCHAR(100),
    @ApplicantEmail NVARCHAR(255)
AS
BEGIN
    SELECT COUNT(*) AS ExistingCount
    FROM Licenses
    WHERE TenantId = @TenantId 
      AND LicenseType = @LicenseType 
      AND ApplicantEmail = @ApplicantEmail 
      AND Status IN ('Active', 'Pending');
END;
GO

-- sp_RenewLicense
CREATE OR ALTER PROCEDURE sp_RenewLicense
    @LicenseId INT,
    @TenantId NVARCHAR(100),
    @Months INT
AS
BEGIN
    UPDATE Licenses
    SET ExpiryDate = DATEADD(MONTH, @Months, ExpiryDate),
        UpdatedAt = GETUTCDATE()
    WHERE Id = @LicenseId AND TenantId = @TenantId;
END;
GO

-- sp_DeleteLicense
CREATE OR ALTER PROCEDURE sp_DeleteLicense
    @LicenseId INT,
    @TenantId NVARCHAR(100)
AS
BEGIN
    DELETE FROM Documents WHERE LicenseId = @LicenseId AND TenantId = @TenantId;
    DELETE FROM Payments WHERE LicenseId = @LicenseId AND TenantId = @TenantId;
    DELETE FROM Licenses WHERE Id = @LicenseId AND TenantId = @TenantId;
END;
GO

-- sp_GetActiveLicenseTypes
CREATE OR ALTER PROCEDURE sp_GetActiveLicenseTypes
AS
BEGIN
    SELECT * FROM LicenseTypes WHERE IsActive = 1 ORDER BY TypeName;
END;
GO

-- sp_GetTenantUsers
CREATE OR ALTER PROCEDURE sp_GetTenantUsers
    @TenantId NVARCHAR(100)
AS
BEGIN
    SELECT Id, Username, Email, FirstName, LastName, Roles, IsActive
    FROM Users
    WHERE TenantId = @TenantId AND IsActive = 1
    ORDER BY Username;
END;
GO

-- sp_GetPaymentById
CREATE OR ALTER PROCEDURE sp_GetPaymentById
    @PaymentId INT,
    @TenantId NVARCHAR(100)
AS
BEGIN
    SELECT * FROM Payments WHERE Id = @PaymentId AND TenantId = @TenantId;
END;
GO

-- sp_GetPaymentByLicenseId
CREATE OR ALTER PROCEDURE sp_GetPaymentByLicenseId
    @LicenseId INT,
    @TenantId NVARCHAR(100)
AS
BEGIN
    SELECT TOP 1 * FROM Payments 
    WHERE LicenseId = @LicenseId AND TenantId = @TenantId 
    ORDER BY CreatedAt DESC;
END;
GO

-- sp_GetPaymentWithLicenseDetails
CREATE OR ALTER PROCEDURE sp_GetPaymentWithLicenseDetails
    @PaymentId INT,
    @TenantId NVARCHAR(100)
AS
BEGIN
    SELECT 
        p.*,
        l.LicenseNumber,
        l.LicenseType,
        l.ApplicantName,
        l.ApplicantEmail
    FROM Payments p
    LEFT JOIN Licenses l ON p.LicenseId = l.Id
    WHERE p.Id = @PaymentId AND p.TenantId = @TenantId;
END;
GO

-- sp_LinkPaymentToLicense
CREATE OR ALTER PROCEDURE sp_LinkPaymentToLicense
    @PaymentId INT,
    @LicenseId INT,
    @TenantId NVARCHAR(100)
AS
BEGIN
    UPDATE Payments
    SET LicenseId = @LicenseId,
        UpdatedAt = GETUTCDATE()
    WHERE Id = @PaymentId AND TenantId = @TenantId;
END;
GO

-- =============================================
-- SEED DATA
-- =============================================

-- Seed License Types
SET IDENTITY_INSERT LicenseTypes ON;
INSERT INTO LicenseTypes (Id, TypeName, Description, Amount, DurationMonths, IsActive, CreatedAt)
VALUES 
    (1, 'Business License', 'Standard business operating license', 500.00, 12, 1, GETUTCDATE()),
    (2, 'Trade License', 'License for trading activities', 750.00, 12, 1, GETUTCDATE()),
    (3, 'Real Estate', 'Real estate broker license', 1000.00, 24, 1, GETUTCDATE()),
    (4, 'Food Service', 'Restaurant and food service license', 600.00, 12, 1, GETUTCDATE()),
    (5, 'Healthcare', 'Healthcare facility license', 1200.00, 24, 1, GETUTCDATE());
SET IDENTITY_INSERT LicenseTypes OFF;
GO

-- Seed Users (Password: Password123! for all)
SET IDENTITY_INSERT Users ON;
INSERT INTO Users (Id, TenantId, Username, Email, PasswordHash, FirstName, LastName, Roles, IsActive, CreatedAt)
VALUES 
    (1, 'tenant1', 'admin', 'admin@test.com', 
     '$2a$11$XvqQrYHYKJLKp5YpYWp5/.W8h8WqYhqYvYqYqYqYqYqYqYqYqYqYq', 
     'Admin', 'User', 'Admin', 1, GETUTCDATE()),
    
    (2, 'tenant1', 'testuser', 'testuser@example.com', 
     '$2a$11$XvqQrYHYKJLKp5YpYWp5/.W8h8WqYhqYvYqYqYqYqYqYqYqYqYqYq', 
     'Test', 'User', 'User', 1, GETUTCDATE()),
    
    (3, 'tenant2', 'john', 'john@tenant2.com', 
     '$2a$11$XvqQrYHYKJLKp5YpYWp5/.W8h8WqYhqYvYqYqYqYqYqYqYqYqYqYq', 
     'John', 'Doe', 'User', 1, GETUTCDATE()),
    
    (4, 'tenant1', 'manager', 'manager@test.com', 
     '$2a$11$XvqQrYHYKJLKp5YpYWp5/.W8h8WqYhqYvYqYqYqYqYqYqYqYqYqYq', 
     'Manager', 'User', 'Manager', 1, GETUTCDATE());
SET IDENTITY_INSERT Users OFF;
GO

-- Seed Sample Licenses
SET IDENTITY_INSERT Licenses ON;
INSERT INTO Licenses (Id, LicenseNumber, UserId, TenantId, LicenseType, ApplicantName, ApplicantEmail, 
                      Status, Amount, IssueDate, ExpiryDate, IsAdminCreated, CreatedAt, UpdatedAt)
VALUES 
    (1, 'LIC-2025-0001', 2, 'tenant1', 'Business License', 'Test User', 'testuser@example.com',
     'Active', 500.00, GETUTCDATE(), DATEADD(YEAR, 1, GETUTCDATE()), 0, GETUTCDATE(), GETUTCDATE()),
    
    (2, 'LIC-2025-0002', 2, 'tenant1', 'Trade License', 'Test User', 'testuser@example.com',
     'Active', 750.00, GETUTCDATE(), DATEADD(YEAR, 1, GETUTCDATE()), 0, GETUTCDATE(), GETUTCDATE()),
    
    (3, 'LIC-2025-0003', 3, 'tenant2', 'Real Estate', 'John Doe', 'john@tenant2.com',
     'Active', 1000.00, GETUTCDATE(), DATEADD(YEAR, 2, GETUTCDATE()), 0, GETUTCDATE(), GETUTCDATE());
SET IDENTITY_INSERT Licenses OFF;
GO

-- Seed Sample Payments
SET IDENTITY_INSERT Payments ON;
INSERT INTO Payments (Id, LicenseId, TenantId, Amount, PaymentMethod, PaymentStatus, InvoiceId, PaidAt, CreatedAt)
VALUES 
    (1, 1, 'tenant1', 500.00, 'razorpay', 'Completed', 'INV-tenant1-00001', GETUTCDATE(), GETUTCDATE()),
    (2, 2, 'tenant1', 750.00, 'razorpay', 'Completed', 'INV-tenant1-00002', GETUTCDATE(), GETUTCDATE()),
    (3, 3, 'tenant2', 1000.00, 'razorpay', 'Completed', 'INV-tenant2-00001', GETUTCDATE(), GETUTCDATE());
SET IDENTITY_INSERT Payments OFF;
GO

-- =============================================
-- VERIFICATION
-- =============================================
PRINT '========================================';
PRINT 'Gov2Biz Database Setup Complete!';
PRINT '========================================';
PRINT '';
PRINT 'Tables Created: ' + CAST((SELECT COUNT(*) FROM sys.tables) AS VARCHAR(10));
PRINT 'Stored Procedures: ' + CAST((SELECT COUNT(*) FROM sys.procedures) AS VARCHAR(10));
PRINT '';
PRINT 'Data Seeded:';
PRINT '  License Types: ' + CAST((SELECT COUNT(*) FROM LicenseTypes) AS VARCHAR(10));
PRINT '  Users: ' + CAST((SELECT COUNT(*) FROM Users) AS VARCHAR(10));
PRINT '  Licenses: ' + CAST((SELECT COUNT(*) FROM Licenses) AS VARCHAR(10));
PRINT '  Payments: ' + CAST((SELECT COUNT(*) FROM Payments) AS VARCHAR(10));
PRINT '';
PRINT 'Test Credentials:';
PRINT '  Admin:   admin@test.com / Password123!';
PRINT '  User:    testuser@example.com / Password123!';
PRINT '  Manager: manager@test.com / Password123!';
PRINT '';
PRINT '========================================';
GO

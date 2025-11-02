-- =====================================================
-- Gov2Biz Complete Database Dump
-- Generated: November 2, 2025
-- Database: Gov2Biz
-- Description: Complete schema and data for Gov2Biz Multi-Tenant License Management System
-- =====================================================

USE master;
GO

-- Drop database if exists (use with caution in production!)
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

-- =====================================================
-- TABLE: Tenants
-- Description: Multi-tenant organizations
-- =====================================================
CREATE TABLE Tenants (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    Name NVARCHAR(200) NOT NULL,
    Domain NVARCHAR(100) NULL,
    IsActive BIT DEFAULT 1,
    CreatedAt DATETIME2 DEFAULT GETUTCDATE(),
    UpdatedAt DATETIME2 DEFAULT GETUTCDATE()
);
GO

-- =====================================================
-- TABLE: Users
-- Description: User accounts with role-based access
-- =====================================================
CREATE TABLE Users (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    Username NVARCHAR(100) NOT NULL,
    Email NVARCHAR(255) NOT NULL UNIQUE,
    PasswordHash NVARCHAR(500) NOT NULL,
    Roles NVARCHAR(200) NULL,
    TenantId INT NOT NULL,
    IsActive BIT DEFAULT 1,
    CreatedAt DATETIME2 DEFAULT GETUTCDATE(),
    UpdatedAt DATETIME2 DEFAULT GETUTCDATE(),
    CONSTRAINT FK_Users_Tenants FOREIGN KEY (TenantId) REFERENCES Tenants(Id)
);
GO

CREATE INDEX IX_Users_TenantId ON Users(TenantId);
CREATE INDEX IX_Users_Email ON Users(Email);
GO

-- =====================================================
-- TABLE: LicenseTypes
-- Description: Predefined license types with pricing
-- =====================================================
CREATE TABLE LicenseTypes (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    TypeName NVARCHAR(100) NOT NULL UNIQUE,
    Description NVARCHAR(500) NULL,
    Amount DECIMAL(18,2) NOT NULL,
    DurationMonths INT NOT NULL DEFAULT 12,
    IsActive BIT DEFAULT 1,
    CreatedAt DATETIME2 DEFAULT GETUTCDATE(),
    UpdatedAt DATETIME2 DEFAULT GETUTCDATE()
);
GO

CREATE INDEX IX_LicenseTypes_IsActive ON LicenseTypes(IsActive);
GO

-- =====================================================
-- TABLE: Licenses
-- Description: License applications and records
-- =====================================================
CREATE TABLE Licenses (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    ApplicantName NVARCHAR(200) NOT NULL,
    LicenseType NVARCHAR(100) NOT NULL,
    Status NVARCHAR(50) DEFAULT 'Pending',
    IssueDate DATETIME2 NULL,
    ExpiryDate DATETIME2 NULL,
    Amount DECIMAL(18,2) DEFAULT 0,
    TenantId INT NOT NULL,
    UserId INT NOT NULL,
    CreatedAt DATETIME2 DEFAULT GETUTCDATE(),
    UpdatedAt DATETIME2 DEFAULT GETUTCDATE(),
    CONSTRAINT FK_Licenses_Tenants FOREIGN KEY (TenantId) REFERENCES Tenants(Id),
    CONSTRAINT FK_Licenses_Users FOREIGN KEY (UserId) REFERENCES Users(Id)
);
GO

CREATE INDEX IX_Licenses_TenantId ON Licenses(TenantId);
CREATE INDEX IX_Licenses_UserId ON Licenses(UserId);
CREATE INDEX IX_Licenses_Status ON Licenses(Status);
GO

-- =====================================================
-- TABLE: LicenseHistory
-- Description: Audit trail for license changes
-- =====================================================
CREATE TABLE LicenseHistory (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    LicenseId INT NOT NULL,
    OldStatus NVARCHAR(50) NULL,
    NewStatus NVARCHAR(50) NOT NULL,
    ChangedBy INT NOT NULL,
    Comments NVARCHAR(MAX) NULL,
    CreatedAt DATETIME2 DEFAULT GETUTCDATE(),
    CONSTRAINT FK_LicenseHistory_Licenses FOREIGN KEY (LicenseId) REFERENCES Licenses(Id),
    CONSTRAINT FK_LicenseHistory_Users FOREIGN KEY (ChangedBy) REFERENCES Users(Id)
);
GO

CREATE INDEX IX_LicenseHistory_LicenseId ON LicenseHistory(LicenseId);
GO

-- =====================================================
-- TABLE: Payments
-- Description: Payment transactions
-- =====================================================
CREATE TABLE Payments (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    LicenseId INT NOT NULL,
    Amount DECIMAL(18,2) NOT NULL,
    PaymentMethod NVARCHAR(50) NULL,
    PaymentStatus NVARCHAR(50) DEFAULT 'Pending',
    TransactionId NVARCHAR(200) NULL,
    TenantId INT NOT NULL,
    CreatedAt DATETIME2 DEFAULT GETUTCDATE(),
    UpdatedAt DATETIME2 DEFAULT GETUTCDATE(),
    CONSTRAINT FK_Payments_Licenses FOREIGN KEY (LicenseId) REFERENCES Licenses(Id),
    CONSTRAINT FK_Payments_Tenants FOREIGN KEY (TenantId) REFERENCES Tenants(Id)
);
GO

CREATE INDEX IX_Payments_LicenseId ON Payments(LicenseId);
CREATE INDEX IX_Payments_TenantId ON Payments(TenantId);
GO

-- =====================================================
-- TABLE: Notifications
-- Description: System notifications
-- =====================================================
CREATE TABLE Notifications (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    UserId INT NOT NULL,
    Type NVARCHAR(50) NOT NULL,
    Title NVARCHAR(200) NOT NULL,
    Message NVARCHAR(MAX) NOT NULL,
    IsRead BIT DEFAULT 0,
    CreatedAt DATETIME2 DEFAULT GETUTCDATE(),
    CONSTRAINT FK_Notifications_Users FOREIGN KEY (UserId) REFERENCES Users(Id)
);
GO

CREATE INDEX IX_Notifications_UserId ON Notifications(UserId);
CREATE INDEX IX_Notifications_IsRead ON Notifications(IsRead);
GO

-- =====================================================
-- STORED PROCEDURES
-- =====================================================

-- Procedure: Create License
CREATE OR ALTER PROCEDURE sp_CreateLicense
    @ApplicantName NVARCHAR(200),
    @LicenseType NVARCHAR(100),
    @Status NVARCHAR(50),
    @IssueDate DATETIME2,
    @ExpiryDate DATETIME2,
    @Amount DECIMAL(18,2),
    @TenantId INT,
    @UserId INT,
    @NewId INT OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    
    INSERT INTO Licenses (ApplicantName, LicenseType, Status, IssueDate, ExpiryDate, Amount, TenantId, UserId, CreatedAt, UpdatedAt)
    VALUES (@ApplicantName, @LicenseType, @Status, @IssueDate, @ExpiryDate, @Amount, @TenantId, @UserId, GETUTCDATE(), GETUTCDATE());
    
    SET @NewId = SCOPE_IDENTITY();
    
    -- Create history record
    INSERT INTO LicenseHistory (LicenseId, OldStatus, NewStatus, ChangedBy, Comments, CreatedAt)
    VALUES (@NewId, NULL, @Status, @UserId, 'License created', GETUTCDATE());
END
GO

-- Procedure: Get Licenses by Tenant
CREATE OR ALTER PROCEDURE sp_GetLicensesByTenant
    @TenantId INT
AS
BEGIN
    SET NOCOUNT ON;
    
    SELECT 
        l.Id,
        l.ApplicantName,
        l.LicenseType,
        l.Status,
        l.IssueDate,
        l.ExpiryDate,
        l.Amount,
        l.TenantId,
        l.UserId,
        l.CreatedAt,
        l.UpdatedAt,
        u.Username,
        u.Email
    FROM Licenses l
    INNER JOIN Users u ON l.UserId = u.Id
    WHERE l.TenantId = @TenantId
    ORDER BY l.CreatedAt DESC;
END
GO

-- Procedure: Get License by ID
CREATE OR ALTER PROCEDURE sp_GetLicenseById
    @Id INT,
    @TenantId INT
AS
BEGIN
    SET NOCOUNT ON;
    
    SELECT 
        l.Id,
        l.ApplicantName,
        l.LicenseType,
        l.Status,
        l.IssueDate,
        l.ExpiryDate,
        l.Amount,
        l.TenantId,
        l.UserId,
        l.CreatedAt,
        l.UpdatedAt,
        u.Username,
        u.Email
    FROM Licenses l
    INNER JOIN Users u ON l.UserId = u.Id
    WHERE l.Id = @Id AND l.TenantId = @TenantId;
END
GO

-- Procedure: Create Payment
CREATE OR ALTER PROCEDURE sp_CreatePayment
    @LicenseId INT,
    @Amount DECIMAL(18,2),
    @PaymentMethod NVARCHAR(50),
    @PaymentStatus NVARCHAR(50),
    @TransactionId NVARCHAR(200),
    @TenantId INT,
    @NewId INT OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    
    INSERT INTO Payments (LicenseId, Amount, PaymentMethod, PaymentStatus, TransactionId, TenantId, CreatedAt, UpdatedAt)
    VALUES (@LicenseId, @Amount, @PaymentMethod, @PaymentStatus, @TransactionId, @TenantId, GETUTCDATE(), GETUTCDATE());
    
    SET @NewId = SCOPE_IDENTITY();
END
GO

-- Procedure: Update Payment Status
CREATE OR ALTER PROCEDURE sp_UpdatePaymentStatus
    @PaymentId INT,
    @PaymentStatus NVARCHAR(50)
AS
BEGIN
    SET NOCOUNT ON;
    
    UPDATE Payments
    SET PaymentStatus = @PaymentStatus, UpdatedAt = GETUTCDATE()
    WHERE Id = @PaymentId;
END
GO

-- =====================================================
-- SEED DATA
-- =====================================================

-- Insert Tenants
SET IDENTITY_INSERT Tenants ON;
INSERT INTO Tenants (Id, Name, Domain, IsActive, CreatedAt, UpdatedAt) VALUES
(1, 'TechCorp Solutions', 'techcorp.com', 1, GETUTCDATE(), GETUTCDATE()),
(2, 'Global Enterprises', 'globalent.com', 1, GETUTCDATE(), GETUTCDATE());
SET IDENTITY_INSERT Tenants OFF;
GO

-- Insert Users
-- Password for all users: Password123! (hashed with BCrypt)
SET IDENTITY_INSERT Users ON;
INSERT INTO Users (Id, Username, Email, PasswordHash, Roles, TenantId, IsActive, CreatedAt, UpdatedAt) VALUES
(1, 'admin', 'admin@test.com', '$2a$11$qV4HkxRQT5mYqQY8nRz.XOqHpVGIUqD5fYHQGcF3PuYKx3W6JxQrG', 'Admin', 1, 1, GETUTCDATE(), GETUTCDATE()),
(2, 'user', 'user@test.com', '$2a$11$qV4HkxRQT5mYqQY8nRz.XOqHpVGIUqD5fYHQGcF3PuYKx3W6JxQrG', 'User', 1, 1, GETUTCDATE(), GETUTCDATE()),
(3, 'audit', 'audit@test.com', '$2a$11$qV4HkxRQT5mYqQY8nRz.XOqHpVGIUqD5fYHQGcF3PuYKx3W6JxQrG', 'Admin,Auditor', 1, 1, GETUTCDATE(), GETUTCDATE()),
(4, 'admin2', 'admin2@test.com', '$2a$11$qV4HkxRQT5mYqQY8nRz.XOqHpVGIUqD5fYHQGcF3PuYKx3W6JxQrG', 'Admin', 2, 1, GETUTCDATE(), GETUTCDATE()),
(5, 'user2', 'user2@test.com', '$2a$11$qV4HkxRQT5mYqQY8nRz.XOqHpVGIUqD5fYHQGcF3PuYKx3W6JxQrG', 'User', 2, 1, GETUTCDATE(), GETUTCDATE());
SET IDENTITY_INSERT Users OFF;
GO

-- Insert License Types (10 Professional Types)
SET IDENTITY_INSERT LicenseTypes ON;
INSERT INTO LicenseTypes (Id, TypeName, Description, Amount, DurationMonths, IsActive, CreatedAt, UpdatedAt) VALUES
(1, 'Business', 'General Business License for commercial activities', 250.00, 12, 1, GETUTCDATE(), GETUTCDATE()),
(2, 'Professional', 'Professional services license (consultants, lawyers, etc.)', 500.00, 12, 1, GETUTCDATE(), GETUTCDATE()),
(3, 'Trade', 'Trade and craft license (plumbers, electricians, etc.)', 350.00, 12, 1, GETUTCDATE(), GETUTCDATE()),
(4, 'Construction', 'Construction and contractor license', 750.00, 12, 1, GETUTCDATE(), GETUTCDATE()),
(5, 'Food Service', 'Food service and restaurant license', 400.00, 12, 1, GETUTCDATE(), GETUTCDATE()),
(6, 'Retail', 'Retail store and shop license', 300.00, 12, 1, GETUTCDATE(), GETUTCDATE()),
(7, 'Healthcare', 'Healthcare facility license', 1000.00, 12, 1, GETUTCDATE(), GETUTCDATE()),
(8, 'Education', 'Educational institution license', 600.00, 12, 1, GETUTCDATE(), GETUTCDATE()),
(9, 'Transportation', 'Transportation and logistics license', 450.00, 12, 1, GETUTCDATE(), GETUTCDATE()),
(10, 'Real Estate', 'Real estate and property management license', 550.00, 12, 1, GETUTCDATE(), GETUTCDATE());
SET IDENTITY_INSERT LicenseTypes OFF;
GO

-- Insert Sample Licenses
SET IDENTITY_INSERT Licenses ON;
INSERT INTO Licenses (Id, ApplicantName, LicenseType, Status, IssueDate, ExpiryDate, Amount, TenantId, UserId, CreatedAt, UpdatedAt) VALUES
(1, 'John Doe', 'Business', 'Active', GETUTCDATE(), DATEADD(YEAR, 1, GETUTCDATE()), 250.00, 1, 2, GETUTCDATE(), GETUTCDATE()),
(2, 'Jane Smith', 'Professional', 'Pending', NULL, NULL, 500.00, 1, 2, GETUTCDATE(), GETUTCDATE()),
(3, 'Bob Johnson', 'Construction', 'Active', GETUTCDATE(), DATEADD(YEAR, 1, GETUTCDATE()), 750.00, 2, 5, GETUTCDATE(), GETUTCDATE());
SET IDENTITY_INSERT Licenses OFF;
GO

-- Insert License History
SET IDENTITY_INSERT LicenseHistory ON;
INSERT INTO LicenseHistory (Id, LicenseId, OldStatus, NewStatus, ChangedBy, Comments, CreatedAt) VALUES
(1, 1, NULL, 'Pending', 2, 'License created', GETUTCDATE()),
(2, 1, 'Pending', 'Active', 1, 'License approved by admin', GETUTCDATE()),
(3, 2, NULL, 'Pending', 2, 'License created', GETUTCDATE()),
(4, 3, NULL, 'Active', 4, 'License created and approved', GETUTCDATE());
SET IDENTITY_INSERT LicenseHistory OFF;
GO

-- Insert Sample Payments
SET IDENTITY_INSERT Payments ON;
INSERT INTO Payments (Id, LicenseId, Amount, PaymentMethod, PaymentStatus, TransactionId, TenantId, CreatedAt, UpdatedAt) VALUES
(1, 1, 250.00, 'CreditCard', 'Completed', 'TXN-2025-001', 1, GETUTCDATE(), GETUTCDATE()),
(2, 2, 500.00, 'BankTransfer', 'Pending', 'TXN-2025-002', 1, GETUTCDATE(), GETUTCDATE()),
(3, 3, 750.00, 'CreditCard', 'Completed', 'TXN-2025-003', 2, GETUTCDATE(), GETUTCDATE());
SET IDENTITY_INSERT Payments OFF;
GO

PRINT 'Gov2Biz database created and seeded successfully!';
PRINT '';
PRINT '==============================================';
PRINT 'DEFAULT LOGIN CREDENTIALS:';
PRINT '==============================================';
PRINT 'Admin User (Tenant 1): admin@test.com / Password123!';
PRINT 'Regular User (Tenant 1): user@test.com / Password123!';
PRINT 'Auditor User (Tenant 1): audit@test.com / Password123!';
PRINT 'Admin User (Tenant 2): admin2@test.com / Password123!';
PRINT 'Regular User (Tenant 2): user2@test.com / Password123!';
PRINT '==============================================';
GO

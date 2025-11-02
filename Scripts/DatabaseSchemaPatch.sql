-- =====================================================
-- Database Schema Patch for Gov2Biz
-- Adds missing columns for compatibility with stored procedures
-- =====================================================

USE Gov2Biz;
GO

-- Add missing columns to Users table
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Users') AND name = 'UpdatedAt')
BEGIN
    ALTER TABLE Users ADD UpdatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE();
    PRINT 'Added UpdatedAt column to Users table.';
END

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Users') AND name = 'LastLoginAt')
BEGIN
    ALTER TABLE Users ADD LastLoginAt DATETIME2 NULL;
    PRINT 'Added LastLoginAt column to Users table.';
END

-- Add missing columns to Payments table
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Payments') AND name = 'LicenseId')
BEGIN
    ALTER TABLE Payments ADD LicenseId INT NULL;
    PRINT 'Added LicenseId column to Payments table.';
END

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Payments') AND name = 'PaymentMethod')
BEGIN
    ALTER TABLE Payments ADD PaymentMethod NVARCHAR(100) NULL;
    PRINT 'Added PaymentMethod column to Payments table.';
END

-- Add missing columns to Licenses table
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Licenses') AND name = 'UserId')
BEGIN
    ALTER TABLE Licenses ADD UserId INT NULL;
    PRINT 'Added UserId column to Licenses table.';
END

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Licenses') AND name = 'IssueDate')
BEGIN
    ALTER TABLE Licenses ADD IssueDate DATETIME2 NULL;
    PRINT 'Added IssueDate column to Licenses table.';
END

-- Create Tenants table if missing
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Tenants')
BEGIN
    CREATE TABLE Tenants (
        Id INT IDENTITY(1,1) PRIMARY KEY,
        TenantId NVARCHAR(50) NOT NULL UNIQUE,
        Name NVARCHAR(200) NOT NULL,
        IsActive BIT NOT NULL DEFAULT 1,
        CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE()
    );
    PRINT 'Created Tenants table.';
    
    -- Add default tenants
    INSERT INTO Tenants (TenantId, Name) VALUES 
        ('SYSTEM', 'System Administration'),
        ('AUDIT-TENANT', 'Audit Department'),
        ('FINANCE-TENANT', 'Finance Department'),
        ('HR-TENANT', 'Human Resources');
    PRINT 'Added default tenants.';
END

PRINT 'Database schema patch completed successfully.';
GO
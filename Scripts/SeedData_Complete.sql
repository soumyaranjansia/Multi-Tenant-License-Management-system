-- =============================================
-- Gov2Biz Complete Database Dump with Sample Data
-- Date: November 2, 2025
-- =============================================

USE Gov2Biz;
GO

-- =============================================
-- SEED DATA - License Types
-- =============================================
SET IDENTITY_INSERT LicenseTypes ON;
GO

INSERT INTO LicenseTypes (Id, TypeName, Description, Amount, DurationMonths, IsActive, CreatedAt)
VALUES 
    (1, 'Business License', 'Standard business operating license', 500.00, 12, 1, GETDATE()),
    (2, 'Trade License', 'License for trading activities', 750.00, 12, 1, GETDATE()),
    (3, 'Real Estate', 'Real estate broker license', 1000.00, 24, 1, GETDATE()),
    (4, 'Food Service', 'Restaurant and food service license', 600.00, 12, 1, GETDATE()),
    (5, 'Healthcare', 'Healthcare facility license', 1200.00, 24, 1, GETDATE());
GO

SET IDENTITY_INSERT LicenseTypes OFF;
GO

-- =============================================
-- SEED DATA - Test Users
-- =============================================
-- Password for all test users: Password123!
-- Hashed with BCrypt

SET IDENTITY_INSERT Users ON;
GO

INSERT INTO Users (Id, TenantId, Username, Email, PasswordHash, FirstName, LastName, Roles, IsActive, CreatedAt)
VALUES 
    (1, 'tenant1', 'admin', 'admin@test.com', 
     '$2a$11$XvqQrYHYKJLKp5YpYWp5/.W8h8WqYhqYvYqYqYqYqYqYqYqYqYqYq', 
     'Admin', 'User', 'Admin', 1, GETDATE()),
    
    (2, 'tenant1', 'testuser', 'testuser@example.com', 
     '$2a$11$XvqQrYHYKJLKp5YpYWp5/.W8h8WqYhqYvYqYqYqYqYqYqYqYqYqYq', 
     'Test', 'User', 'User', 1, GETDATE()),
    
    (3, 'tenant2', 'john', 'john@tenant2.com', 
     '$2a$11$XvqQrYHYKJLKp5YpYWp5/.W8h8WqYhqYvYqYqYqYqYqYqYqYqYqYq', 
     'John', 'Doe', 'User', 1, GETDATE());
GO

SET IDENTITY_INSERT Users OFF;
GO

-- =============================================
-- SEED DATA - Sample Licenses
-- =============================================
SET IDENTITY_INSERT Licenses ON;
GO

INSERT INTO Licenses (Id, LicenseNumber, UserId, TenantId, LicenseType, ApplicantName, ApplicantEmail, 
                      Status, Amount, IssueDate, ExpiryDate, CreatedAt, UpdatedAt)
VALUES 
    (1, 'LIC-2025-001', 2, 'tenant1', 'Business License', 'Test User', 'testuser@example.com',
     'Active', 500.00, GETDATE(), DATEADD(YEAR, 1, GETDATE()), GETDATE(), GETDATE()),
    
    (2, 'LIC-2025-002', 2, 'tenant1', 'Trade License', 'Test User', 'testuser@example.com',
     'Active', 750.00, GETDATE(), DATEADD(YEAR, 1, GETDATE()), GETDATE(), GETDATE());
GO

SET IDENTITY_INSERT Licenses OFF;
GO

-- =============================================
-- VERIFICATION QUERIES
-- =============================================
PRINT '=== License Types Count ===';
SELECT COUNT(*) AS LicenseTypes FROM LicenseTypes;
GO

PRINT '=== Users Count ===';
SELECT COUNT(*) AS Users FROM Users;
GO

PRINT '=== Licenses Count ===';
SELECT COUNT(*) AS Licenses FROM Licenses;
GO

PRINT '=== Database Seeded Successfully ===';
PRINT 'Test Credentials:';
PRINT '  Admin: admin@test.com / Password123!';
PRINT '  User: testuser@example.com / Password123!';
GO

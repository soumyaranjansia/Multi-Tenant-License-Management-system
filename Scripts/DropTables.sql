-- Drop all tables in reverse order of dependencies
-- Use with caution - this will delete all data
-- ==============================================

USE Gov2Biz;
GO

PRINT 'Dropping tables...';

-- Drop tables in reverse order of dependencies
IF OBJECT_ID('dbo.Notifications', 'U') IS NOT NULL
    DROP TABLE dbo.Notifications;
    
IF OBJECT_ID('dbo.Payments', 'U') IS NOT NULL
    DROP TABLE dbo.Payments;
    
IF OBJECT_ID('dbo.LicenseHistory', 'U') IS NOT NULL
    DROP TABLE dbo.LicenseHistory;
    
IF OBJECT_ID('dbo.Licenses', 'U') IS NOT NULL
    DROP TABLE dbo.Licenses;
    
IF OBJECT_ID('dbo.Users', 'U') IS NOT NULL
    DROP TABLE dbo.Users;
    
IF OBJECT_ID('dbo.Tenants', 'U') IS NOT NULL
    DROP TABLE dbo.Tenants;

-- Drop stored procedures
IF OBJECT_ID('dbo.sp_CreateLicense', 'P') IS NOT NULL
    DROP PROCEDURE dbo.sp_CreateLicense;
    
IF OBJECT_ID('dbo.sp_GetLicenseById', 'P') IS NOT NULL
    DROP PROCEDURE dbo.sp_GetLicenseById;
    
IF OBJECT_ID('dbo.sp_RenewLicense', 'P') IS NOT NULL
    DROP PROCEDURE dbo.sp_RenewLicense;
    
IF OBJECT_ID('dbo.sp_GetLicensesByTenant', 'P') IS NOT NULL
    DROP PROCEDURE dbo.sp_GetLicensesByTenant;

GO

PRINT 'All tables and stored procedures dropped successfully.';

-- Gov2Biz Database Schema
-- Multi-Tenant License Management System
-- ==============================================

USE Gov2Biz;
GO

-- Tenants Table
-- Stores tenant organization information
CREATE TABLE Tenants (
    TenantId NVARCHAR(50) NOT NULL PRIMARY KEY,
    Name NVARCHAR(200) NOT NULL,
    CreatedAt DATETIME2 DEFAULT SYSUTCDATETIME(),
    IsActive BIT DEFAULT 1,
    CONSTRAINT CK_Tenants_TenantId CHECK (TenantId LIKE '[A-Za-z0-9-]%' AND LEN(TenantId) BETWEEN 1 AND 50)
);

CREATE INDEX IX_Tenants_IsActive ON Tenants(IsActive);

-- Users Table
-- Stores user credentials and roles per tenant
CREATE TABLE Users (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    Username NVARCHAR(200) NOT NULL,
    Email NVARCHAR(256) NOT NULL,
    PasswordHash NVARCHAR(512) NOT NULL,
    Roles NVARCHAR(200) NOT NULL, -- CSV: Admin,Applicant,AgencyUser
    TenantId NVARCHAR(50) NOT NULL,
    CreatedAt DATETIME2 DEFAULT SYSUTCDATETIME(),
    IsActive BIT DEFAULT 1,
    CONSTRAINT FK_Users_Tenants FOREIGN KEY (TenantId) REFERENCES Tenants(TenantId) ON DELETE CASCADE,
    CONSTRAINT UQ_Users_Email_Tenant UNIQUE (Email, TenantId)
);

CREATE INDEX IX_Users_TenantId ON Users(TenantId);
CREATE INDEX IX_Users_Username ON Users(Username);

-- Licenses Table
-- Core license entity with multi-tenant isolation
CREATE TABLE Licenses (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    LicenseNumber NVARCHAR(100) NOT NULL UNIQUE,
    ApplicantName NVARCHAR(256) NOT NULL,
    ApplicantEmail NVARCHAR(256) NOT NULL,
    LicenseType NVARCHAR(100) NOT NULL,
    Status NVARCHAR(50) NOT NULL, -- Pending, Active, Expired, Suspended, Renewed
    ExpiryDate DATETIME2 NOT NULL,
    Metadata NVARCHAR(MAX) NULL, -- JSON metadata
    TenantId NVARCHAR(50) NOT NULL,
    CreatedAt DATETIME2 DEFAULT SYSUTCDATETIME(),
    UpdatedAt DATETIME2 DEFAULT SYSUTCDATETIME(),
    CONSTRAINT FK_Licenses_Tenants FOREIGN KEY (TenantId) REFERENCES Tenants(TenantId) ON DELETE CASCADE
);

CREATE INDEX IX_Licenses_TenantId ON Licenses(TenantId);
CREATE INDEX IX_Licenses_Status ON Licenses(Status);
CREATE INDEX IX_Licenses_ExpiryDate ON Licenses(ExpiryDate);
CREATE INDEX IX_Licenses_LicenseNumber ON Licenses(LicenseNumber);

-- LicenseHistory Table
-- Audit trail for all license changes
CREATE TABLE LicenseHistory (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    LicenseId INT NOT NULL,
    Timestamp DATETIME2 DEFAULT SYSUTCDATETIME(),
    Action NVARCHAR(200) NOT NULL, -- Created, Renewed, Suspended, Expired, etc.
    PerformedBy NVARCHAR(256) NULL,
    Details NVARCHAR(MAX) NULL,
    CONSTRAINT FK_LicenseHistory_Licenses FOREIGN KEY (LicenseId) REFERENCES Licenses(Id) ON DELETE CASCADE
);

CREATE INDEX IX_LicenseHistory_LicenseId ON LicenseHistory(LicenseId);
CREATE INDEX IX_LicenseHistory_Timestamp ON LicenseHistory(Timestamp DESC);

-- Payments Table
-- Payment records linked to tenants
CREATE TABLE Payments (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    PaymentReference NVARCHAR(200) NOT NULL UNIQUE,
    Amount DECIMAL(18,2) NOT NULL,
    Currency NVARCHAR(10) NOT NULL DEFAULT 'USD',
    Status NVARCHAR(50) NOT NULL, -- Pending, Succeeded, Failed, Refunded
    Metadata NVARCHAR(MAX) NULL, -- JSON metadata (licenseId, etc.)
    TenantId NVARCHAR(50) NOT NULL,
    CreatedAt DATETIME2 DEFAULT SYSUTCDATETIME(),
    UpdatedAt DATETIME2 DEFAULT SYSUTCDATETIME(),
    CONSTRAINT FK_Payments_Tenants FOREIGN KEY (TenantId) REFERENCES Tenants(TenantId)
);

CREATE INDEX IX_Payments_TenantId ON Payments(TenantId);
CREATE INDEX IX_Payments_Status ON Payments(Status);
CREATE INDEX IX_Payments_PaymentReference ON Payments(PaymentReference);

-- Notifications Table
-- Notification queue and history
CREATE TABLE Notifications (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    ToAddresses NVARCHAR(MAX) NOT NULL, -- JSON array or CSV
    Subject NVARCHAR(512) NOT NULL,
    Body NVARCHAR(MAX) NOT NULL,
    Metadata NVARCHAR(MAX) NULL, -- JSON metadata
    Status NVARCHAR(50) NOT NULL DEFAULT 'Pending', -- Pending, Sent, Failed
    CreatedAt DATETIME2 DEFAULT SYSUTCDATETIME(),
    SentAt DATETIME2 NULL,
    ErrorMessage NVARCHAR(MAX) NULL
);

CREATE INDEX IX_Notifications_Status ON Notifications(Status);
CREATE INDEX IX_Notifications_CreatedAt ON Notifications(CreatedAt DESC);

GO

PRINT 'Database schema created successfully.';

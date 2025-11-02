-- Create LicenseTypes Table
-- This table stores the available license types and their associated fees
-- ==============================================

USE Gov2Biz;
GO

-- Create LicenseTypes table
IF OBJECT_ID('dbo.LicenseTypes', 'U') IS NOT NULL
    DROP TABLE dbo.LicenseTypes;
GO

CREATE TABLE LicenseTypes (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    TypeName NVARCHAR(100) NOT NULL UNIQUE,
    Description NVARCHAR(500) NULL,
    Amount DECIMAL(18,2) NOT NULL,
    DurationMonths INT NOT NULL DEFAULT 12,
    IsActive BIT DEFAULT 1,
    CreatedAt DATETIME2 DEFAULT SYSUTCDATETIME(),
    UpdatedAt DATETIME2 DEFAULT SYSUTCDATETIME()
);

CREATE INDEX IX_LicenseTypes_IsActive ON LicenseTypes(IsActive);
CREATE INDEX IX_LicenseTypes_TypeName ON LicenseTypes(TypeName);

-- Insert default license types
INSERT INTO LicenseTypes (TypeName, Description, Amount, DurationMonths) VALUES
('Business', 'General Business License for commercial activities', 250.00, 12),
('Professional', 'Professional practice license (doctors, lawyers, etc.)', 500.00, 12),
('Trade', 'Trade license for skilled trades (electrician, plumber, etc.)', 350.00, 12),
('Construction', 'Construction and contractor license', 750.00, 12),
('Food Service', 'Food service and restaurant license', 400.00, 12),
('Retail', 'Retail business license', 300.00, 12),
('Healthcare', 'Healthcare facility license', 1000.00, 12),
('Education', 'Educational institution license', 600.00, 12),
('Transportation', 'Transportation and logistics license', 450.00, 12),
('Real Estate', 'Real estate broker/agent license', 550.00, 12);

GO

PRINT 'LicenseTypes table created and populated successfully.';

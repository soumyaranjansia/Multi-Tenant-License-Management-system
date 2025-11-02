-- Add Amount column to Licenses table
-- This migration adds the Amount field to track license fees

USE Gov2Biz;
GO

-- Add Amount column if it doesn't exist
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Licenses') AND name = 'Amount')
BEGIN
    ALTER TABLE Licenses
    ADD Amount DECIMAL(18,2) NOT NULL DEFAULT 0.00;
    
    PRINT 'Amount column added to Licenses table';
END
ELSE
BEGIN
    PRINT 'Amount column already exists in Licenses table';
END
GO

-- Update existing licenses with default amounts based on license type
UPDATE Licenses
SET Amount = CASE 
    WHEN LicenseType LIKE '%Business%' THEN 100.00
    WHEN LicenseType LIKE '%Professional%' THEN 150.00
    WHEN LicenseType LIKE '%Vendor%' THEN 200.00
    ELSE 100.00
END
WHERE Amount = 0.00;

PRINT 'Updated existing licenses with default amounts';
GO

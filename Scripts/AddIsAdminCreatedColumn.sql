-- Migration: Add IsAdminCreated column to Licenses table
-- This will help distinguish between admin-created and user-created licenses

USE Gov2Biz;
GO

-- Add IsAdminCreated column to Licenses table
IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS 
               WHERE TABLE_NAME = 'Licenses' AND COLUMN_NAME = 'IsAdminCreated')
BEGIN
    ALTER TABLE Licenses 
    ADD IsAdminCreated BIT NOT NULL DEFAULT 0;
    
    PRINT 'IsAdminCreated column added to Licenses table';
END
ELSE
BEGIN
    PRINT 'IsAdminCreated column already exists in Licenses table';
END
GO

-- Update existing licenses to mark them as admin-created if created by admin users
-- We'll assume audit@test1.com and audit@test3com are admin users based on the pattern
UPDATE Licenses 
SET IsAdminCreated = 1
WHERE Id IN (
    SELECT DISTINCT lh.LicenseId
    FROM LicenseHistory lh
    WHERE lh.Action = 'Created' 
    AND (lh.PerformedBy LIKE 'audit@%' OR lh.PerformedBy LIKE 'admin@%')
);

PRINT 'Updated existing licenses to mark admin-created ones';
GO
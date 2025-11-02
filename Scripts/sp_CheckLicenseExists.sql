-- Stored Procedure: Check License Exists for Tenant
-- Verifies if a license exists and belongs to the specified tenant
CREATE OR ALTER PROCEDURE sp_CheckLicenseExists
    @LicenseId INT,
    @TenantId NVARCHAR(50)
AS
BEGIN
    SET NOCOUNT ON;
    
    SELECT CAST(CASE WHEN EXISTS (
        SELECT 1 FROM Licenses 
        WHERE Id = @LicenseId 
        AND TenantId = @TenantId
    ) THEN 1 ELSE 0 END AS BIT) as LicenseExists;
END
GO
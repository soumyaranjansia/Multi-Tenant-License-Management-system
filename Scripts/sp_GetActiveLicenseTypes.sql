-- Stored Procedure: Get Active License Types
-- Retrieves all active license types with their details
CREATE OR ALTER PROCEDURE sp_GetActiveLicenseTypes
AS
BEGIN
    SET NOCOUNT ON;
    
    SELECT 
        Id, 
        TypeName, 
        Description, 
        Amount, 
        DurationMonths, 
        IsActive, 
        CreatedAt, 
        UpdatedAt 
    FROM LicenseTypes 
    WHERE IsActive = 1 
    ORDER BY TypeName;
END
GO
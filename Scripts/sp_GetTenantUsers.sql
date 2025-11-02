-- Stored Procedure: Get Tenant Users
-- Retrieves all active users for a specific tenant
CREATE OR ALTER PROCEDURE sp_GetTenantUsers
    @TenantId NVARCHAR(50)
AS
BEGIN
    SET NOCOUNT ON;
    
    -- Validate tenant exists
    IF NOT EXISTS (SELECT 1 FROM Tenants WHERE TenantId = @TenantId)
    BEGIN
        THROW 50001, 'Invalid TenantId', 1;
    END
    
    SELECT 
        Id, 
        Username, 
        Email, 
        Roles, 
        TenantId, 
        IsActive,
        CreatedAt,
        UpdatedAt
    FROM Users 
    WHERE TenantId = @TenantId 
    AND IsActive = 1 
    ORDER BY Username;
END
GO
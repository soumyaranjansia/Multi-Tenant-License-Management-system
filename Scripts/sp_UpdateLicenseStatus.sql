-- Stored Procedure: Update License Status
-- Updates the status of a license
CREATE OR ALTER PROCEDURE sp_UpdateLicenseStatus
    @LicenseId INT,
    @Status NVARCHAR(50),
    @TenantId NVARCHAR(50) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    
    DECLARE @sql NVARCHAR(MAX) = 'UPDATE Licenses SET Status = @Status, UpdatedAt = GETUTCDATE() WHERE Id = @LicenseId';
    
    -- Add tenant filter if provided for additional security
    IF @TenantId IS NOT NULL
    BEGIN
        SET @sql = @sql + ' AND TenantId = @TenantId';
    END
    
    EXEC sp_executesql @sql, N'@LicenseId INT, @Status NVARCHAR(50), @TenantId NVARCHAR(50)', @LicenseId, @Status, @TenantId;
    
    SELECT @@ROWCOUNT AS RowsAffected;
END
GO
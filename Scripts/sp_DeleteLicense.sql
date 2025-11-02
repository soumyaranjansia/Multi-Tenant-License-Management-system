-- Stored Procedure: Delete License
-- Safely deletes a license and its associated records
CREATE OR ALTER PROCEDURE sp_DeleteLicense
    @LicenseId INT,
    @TenantId NVARCHAR(50)
AS
BEGIN
    SET NOCOUNT ON;
    
    BEGIN TRANSACTION;
    
    BEGIN TRY
        -- Validate license exists and belongs to tenant
        IF NOT EXISTS (SELECT 1 FROM Licenses WHERE Id = @LicenseId AND TenantId = @TenantId)
        BEGIN
            ROLLBACK TRANSACTION;
            THROW 50001, 'License not found or access denied', 1;
        END
        
        -- Delete associated records first (to maintain referential integrity)
        DELETE FROM LicenseHistory 
        WHERE LicenseId = @LicenseId;
        
        -- Delete associated documents
        DELETE FROM Documents 
        WHERE LicenseId = @LicenseId;
        
        -- Delete the license
        DELETE FROM Licenses 
        WHERE Id = @LicenseId 
        AND TenantId = @TenantId;
        
        COMMIT TRANSACTION;
        
        SELECT 
            @LicenseId as LicenseId,
            'SUCCESS' as Status,
            'License and associated records deleted successfully' as Message;
            
    END TRY
    BEGIN CATCH
        ROLLBACK TRANSACTION;
        
        DECLARE @ErrorMessage NVARCHAR(4000) = ERROR_MESSAGE();
        DECLARE @ErrorSeverity INT = ERROR_SEVERITY();
        DECLARE @ErrorState INT = ERROR_STATE();
        
        RAISERROR(@ErrorMessage, @ErrorSeverity, @ErrorState);
    END CATCH
END
GO
-- Stored Procedure: sp_RenewLicense
-- Renews a license with new expiry date
-- Returns: Success flag
-- ==============================================

USE Gov2Biz;
GO

IF OBJECT_ID('dbo.sp_RenewLicense', 'P') IS NOT NULL
    DROP PROCEDURE dbo.sp_RenewLicense;
GO

CREATE PROCEDURE dbo.sp_RenewLicense
    @LicenseId INT,
    @RenewalDate DATETIME2,
    @PaymentReference NVARCHAR(200),
    @TenantId NVARCHAR(50),
    @PerformedBy NVARCHAR(256) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    BEGIN TRY
        BEGIN TRANSACTION;

        -- Validate license exists and belongs to tenant
        IF NOT EXISTS (SELECT 1 FROM Licenses WHERE Id = @LicenseId AND TenantId = @TenantId)
        BEGIN
            RAISERROR('License not found for tenant', 16, 1);
            RETURN;
        END

        -- Update license
        UPDATE Licenses
        SET 
            ExpiryDate = @RenewalDate,
            Status = 'Active',
            UpdatedAt = SYSUTCDATETIME()
        WHERE Id = @LicenseId AND TenantId = @TenantId;

        -- Add history entry
        INSERT INTO LicenseHistory (LicenseId, Action, PerformedBy, Details)
        VALUES (
            @LicenseId, 
            'Renewed', 
            ISNULL(@PerformedBy, @PaymentReference), 
            CONCAT('Renewed to ', CONVERT(NVARCHAR(50), @RenewalDate, 126), ' via payment ', @PaymentReference)
        );

        COMMIT TRANSACTION;

        -- Return success
        SELECT 1 AS Success, @LicenseId AS LicenseId, @RenewalDate AS NewExpiryDate;

    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0
            ROLLBACK TRANSACTION;
        
        DECLARE @ErrorMessage NVARCHAR(4000) = ERROR_MESSAGE();
        DECLARE @ErrorSeverity INT = ERROR_SEVERITY();
        DECLARE @ErrorState INT = ERROR_STATE();
        
        RAISERROR(@ErrorMessage, @ErrorSeverity, @ErrorState);
    END CATCH
END
GO

PRINT 'sp_RenewLicense created successfully.';

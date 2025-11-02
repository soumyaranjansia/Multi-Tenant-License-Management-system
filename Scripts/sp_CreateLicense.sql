-- Stored Procedure: sp_CreateLicense
-- Creates a new license with tenant isolation
-- Returns: NewId (License ID)
-- ==============================================

USE Gov2Biz;
GO

IF OBJECT_ID('dbo.sp_CreateLicense', 'P') IS NOT NULL
    DROP PROCEDURE dbo.sp_CreateLicense;
GO

CREATE PROCEDURE dbo.sp_CreateLicense
    @ApplicantName NVARCHAR(256),
    @ApplicantEmail NVARCHAR(256),
    @LicenseType NVARCHAR(100),
    @ExpiryDate DATETIME2,
    @TenantId NVARCHAR(50),
    @Metadata NVARCHAR(MAX) = NULL,
    @Amount DECIMAL(18,2) = 100.00,
    @IsAdminCreated BIT = 0
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    BEGIN TRY
        BEGIN TRANSACTION;

        -- Validate tenant exists
        IF NOT EXISTS (SELECT 1 FROM Tenants WHERE TenantId = @TenantId AND IsActive = 1)
        BEGIN
            RAISERROR('Invalid or inactive tenant', 16, 1);
            RETURN;
        END

        DECLARE @NewId INT;
        DECLARE @LicenseNumber NVARCHAR(100);
        DECLARE @Year NVARCHAR(4) = FORMAT(GETUTCDATE(), 'yyyy');
        
        -- Generate unique license number: LIC-YYYY-NNNNNN
        -- Get next sequence number
        DECLARE @NextSeq INT = ISNULL((SELECT MAX(Id) FROM Licenses), 0) + 1;
        SET @LicenseNumber = CONCAT('LIC-', @Year, '-', RIGHT('000000' + CAST(@NextSeq AS NVARCHAR(6)), 6));

        -- Insert license
        INSERT INTO Licenses (
            LicenseNumber, 
            ApplicantName, 
            ApplicantEmail, 
            LicenseType, 
            Status, 
            ExpiryDate, 
            Amount,
            Metadata, 
            TenantId,
            IsAdminCreated,
            CreatedAt,
            UpdatedAt
        )
        VALUES (
            @LicenseNumber,
            @ApplicantName,
            @ApplicantEmail,
            @LicenseType,
            'Pending',
            @ExpiryDate,
            @Amount,
            @Metadata,
            @TenantId,
            @IsAdminCreated,
            SYSUTCDATETIME(),
            SYSUTCDATETIME()
        );

        SET @NewId = SCOPE_IDENTITY();

        -- Add history entry
        INSERT INTO LicenseHistory (LicenseId, Action, PerformedBy, Details)
        VALUES (@NewId, 'Created', @ApplicantEmail, CONCAT('License created for ', @ApplicantName));

        COMMIT TRANSACTION;

        -- Return new ID
        SELECT @NewId AS NewId, @LicenseNumber AS LicenseNumber;

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

PRINT 'sp_CreateLicense created successfully.';

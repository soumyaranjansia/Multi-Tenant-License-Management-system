-- Stored Procedure: sp_GetLicenseById
-- Retrieves a license by ID with tenant isolation
-- Returns: License with history JSON
-- ==============================================

USE Gov2Biz;
GO

IF OBJECT_ID('dbo.sp_GetLicenseById', 'P') IS NOT NULL
    DROP PROCEDURE dbo.sp_GetLicenseById;
GO

CREATE PROCEDURE dbo.sp_GetLicenseById
    @LicenseId INT,
    @TenantId NVARCHAR(50)
AS
BEGIN
    SET NOCOUNT ON;

    -- Return license with history as JSON
    SELECT 
        l.Id,
        l.LicenseNumber,
        l.ApplicantName,
        l.ApplicantEmail,
        l.LicenseType,
        l.Status,
        l.ExpiryDate,
        l.Amount,
        l.Metadata,
        l.TenantId,
        l.CreatedAt,
        l.UpdatedAt,
        (
            SELECT 
                lh.Timestamp,
                lh.Action,
                lh.PerformedBy,
                lh.Details
            FROM LicenseHistory lh
            WHERE lh.LicenseId = l.Id
            ORDER BY lh.Timestamp DESC
            FOR JSON PATH
        ) AS History
    FROM Licenses l
    WHERE l.Id = @LicenseId 
      AND l.TenantId = @TenantId;

END
GO

PRINT 'sp_GetLicenseById created successfully.';

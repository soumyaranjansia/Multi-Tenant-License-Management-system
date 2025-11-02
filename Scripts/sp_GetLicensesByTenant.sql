-- Stored Procedure: sp_GetLicensesByTenant
-- Retrieves all licenses for a tenant
-- Used by Hangfire daily job to find expiring licenses
-- ==============================================

USE Gov2Biz;
GO

IF OBJECT_ID('dbo.sp_GetLicensesByTenant', 'P') IS NOT NULL
    DROP PROCEDURE dbo.sp_GetLicensesByTenant;
GO

CREATE PROCEDURE dbo.sp_GetLicensesByTenant
    @TenantId NVARCHAR(50),
    @Status NVARCHAR(50) = NULL,
    @ExpiringInDays INT = NULL
AS
BEGIN
    SET NOCOUNT ON;

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
        NULL AS History
    FROM Licenses l
    WHERE l.TenantId = @TenantId
      AND (@Status IS NULL OR l.Status = @Status)
      AND (
          @ExpiringInDays IS NULL 
          OR l.ExpiryDate BETWEEN SYSUTCDATETIME() AND DATEADD(DAY, @ExpiringInDays, SYSUTCDATETIME())
      )
    ORDER BY l.ExpiryDate ASC;

END
GO

PRINT 'sp_GetLicensesByTenant created successfully.';

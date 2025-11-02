-- Stored Procedure: Get License Data for Payment Creation
-- Retrieves license and user details for Razorpay invoice generation
CREATE OR ALTER PROCEDURE sp_GetLicenseDataForPayment
    @LicenseId INT,
    @TenantId NVARCHAR(50)
AS
BEGIN
    SET NOCOUNT ON;
    
    SELECT 
        l.Id, 
        l.ApplicantName, 
        l.LicenseType, 
        l.IssueDate, 
        l.ExpiryDate,
        u.Username, 
        u.Email, 
        t.Name as TenantName, 
        ISNULL(lt.TypeName, l.LicenseType) as TypeName, 
        ISNULL(lt.DurationMonths, 12) as DurationMonths
    FROM Licenses l
    INNER JOIN Users u ON l.UserId = u.Id
    INNER JOIN Tenants t ON l.TenantId = t.Id
    LEFT JOIN LicenseTypes lt ON l.LicenseType = lt.TypeName
    WHERE l.Id = @LicenseId 
    AND l.TenantId = @TenantId;
END
GO
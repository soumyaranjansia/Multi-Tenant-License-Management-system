-- Stored Procedure: Get Payment by License ID
-- Retrieves payment details associated with a specific license
CREATE OR ALTER PROCEDURE sp_GetPaymentByLicenseId
    @LicenseId INT,
    @TenantId NVARCHAR(50)
AS
BEGIN
    SET NOCOUNT ON;
    
    SELECT 
        p.Id,
        p.PaymentReference,
        p.Amount,
        p.Currency,
        p.Status,
        p.PaymentMethod,
        p.RazorpayOrderId,
        p.RazorpayPaymentId,
        p.RazorpaySignature,
        p.InvoiceId,
        p.CreatedAt,
        p.UpdatedAt,
        p.TenantId,
        l.Id as LicenseId,
        l.ApplicantName,
        l.ApplicantEmail,
        l.LicenseType,
        l.LicenseNumber
    FROM Payments p
    INNER JOIN Licenses l ON p.LicenseId = l.Id
    WHERE l.Id = @LicenseId 
    AND p.TenantId = @TenantId
    AND l.TenantId = @TenantId;
END
GO
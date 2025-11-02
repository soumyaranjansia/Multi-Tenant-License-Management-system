-- Stored Procedure: Get Payment with License Details for Invoice
-- Retrieves payment and associated license details for invoice generation
CREATE OR ALTER PROCEDURE sp_GetPaymentWithLicenseDetails
    @PaymentId INT,
    @TenantId NVARCHAR(50) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    
    DECLARE @sql NVARCHAR(MAX);
    
    SET @sql = '
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
        l.ApplicantName,
        l.ApplicantEmail,
        l.LicenseType,
        l.CreatedAt as IssueDate,
        l.ExpiryDate,
        l.LicenseNumber
    FROM Payments p
    LEFT JOIN Licenses l ON p.LicenseId = l.Id
    WHERE p.Id = @PaymentId';
    
    -- Add tenant filter if provided
    IF @TenantId IS NOT NULL
    BEGIN
        SET @sql = @sql + ' AND p.TenantId = @TenantId';
    END
    
    EXEC sp_executesql @sql, N'@PaymentId INT, @TenantId NVARCHAR(50)', @PaymentId, @TenantId;
END
GO
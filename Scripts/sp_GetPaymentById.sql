-- Stored Procedure: Get Payment By ID
CREATE OR ALTER PROCEDURE sp_GetPaymentById
    @TenantId NVARCHAR(50),
    @PaymentId INT
AS
BEGIN
    SET NOCOUNT ON;
    
    SELECT 
        Id,
        TenantId,
        LicenseId,
        PaymentNumber,
        Amount,
        Currency,
        Status,
        GatewayTransactionId,
        CreatedAt,
        CompletedAt
    FROM Payments
    WHERE Id = @PaymentId AND TenantId = @TenantId;
END
GO

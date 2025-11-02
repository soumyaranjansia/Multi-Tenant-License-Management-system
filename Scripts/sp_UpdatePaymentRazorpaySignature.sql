-- Stored Procedure: Update Payment Razorpay Signature
-- Updates payment record with Razorpay invoice ID in signature field
CREATE OR ALTER PROCEDURE sp_UpdatePaymentRazorpaySignature
    @PaymentId INT,
    @RazorpayInvoiceId NVARCHAR(255)
AS
BEGIN
    SET NOCOUNT ON;
    
    UPDATE Payments 
    SET RazorpaySignature = @RazorpayInvoiceId,
        UpdatedAt = GETUTCDATE()
    WHERE Id = @PaymentId;
    
    SELECT @@ROWCOUNT AS RowsAffected;
END
GO
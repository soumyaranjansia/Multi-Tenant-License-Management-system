-- Stored Procedure: Update Payment Status (from webhook)
CREATE OR ALTER PROCEDURE sp_UpdatePaymentStatus
    @TransactionId NVARCHAR(100),
    @Status NVARCHAR(20)
AS
BEGIN
    SET NOCOUNT ON;
    
    BEGIN TRANSACTION;
    
    BEGIN TRY
        -- Update payment status
        UPDATE Payments
        SET 
            Status = @Status,
            GatewayTransactionId = @TransactionId,
            CompletedAt = CASE WHEN @Status IN ('Completed', 'Failed') THEN GETUTCDATE() ELSE CompletedAt END
        WHERE GatewayTransactionId = @TransactionId OR PaymentNumber = @TransactionId;
        
        IF @@ROWCOUNT = 0
        BEGIN
            ROLLBACK TRANSACTION;
            THROW 50003, 'Payment not found for transaction', 1;
        END
        
        COMMIT TRANSACTION;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0
            ROLLBACK TRANSACTION;
        THROW;
    END CATCH
END
GO

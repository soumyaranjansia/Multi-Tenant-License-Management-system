-- =====================================================
-- Payment-First Workflow Update
-- Updates sp_CompletePayment to automatically activate licenses after payment
-- =====================================================

USE Gov2Biz;
GO

-- Update sp_CompletePayment to auto-activate licenses
CREATE OR ALTER PROCEDURE sp_CompletePayment
    @PaymentId INT,
    @RazorpayPaymentId NVARCHAR(100),
    @RazorpaySignature NVARCHAR(500),
    @TransactionId NVARCHAR(200)
AS
BEGIN
    SET NOCOUNT ON;
    
    DECLARE @Metadata NVARCHAR(MAX);
    DECLARE @LicenseId INT;
    
    -- Get payment details and associated license
    SELECT @Metadata = ISNULL(Metadata, '{}'), @LicenseId = LicenseId 
    FROM Payments 
    WHERE Id = @PaymentId;
    
    -- Update metadata with transaction ID
    SET @Metadata = REPLACE(@Metadata, '}', ', "transactionId": "' + @TransactionId + '"}');
    
    -- Update payment status
    UPDATE Payments 
    SET 
        Status = 'Completed',
        RazorpayPaymentId = @RazorpayPaymentId,
        RazorpaySignature = @RazorpaySignature,
        Metadata = @Metadata,
        UpdatedAt = GETUTCDATE()
    WHERE Id = @PaymentId;
    
    -- If license is linked, activate it immediately
    IF @LicenseId IS NOT NULL AND @LicenseId > 0
    BEGIN
        -- Update license status to Active (automatic activation after payment)
        UPDATE Licenses 
        SET Status = 'Active', UpdatedAt = GETUTCDATE()
        WHERE Id = @LicenseId;
        
        -- Add history entry
        INSERT INTO LicenseHistory (LicenseId, Timestamp, Action, PerformedBy, Details)
        SELECT 
            @LicenseId,
            GETUTCDATE(),
            'Payment Completed - License Activated',
            l.ApplicantEmail,
            'Payment completed with transaction ID: ' + @TransactionId + '. License automatically activated.'
        FROM Licenses l
        WHERE l.Id = @LicenseId;
        
        PRINT 'License ' + CAST(@LicenseId AS NVARCHAR(10)) + ' activated after payment completion';
    END
    ELSE
    BEGIN
        -- Legacy logic: try to extract license ID from payment reference
        DECLARE @PaymentRef NVARCHAR(200);
        SELECT @PaymentRef = PaymentReference FROM Payments WHERE Id = @PaymentId;
        
        IF @PaymentRef LIKE 'LIC_%' OR @PaymentRef LIKE 'PRE_%'
        BEGIN
            -- Extract license ID from reference formats
            DECLARE @RefLicenseId INT;
            
            IF @PaymentRef LIKE 'LIC_%'
                SET @RefLicenseId = CAST(SUBSTRING(@PaymentRef, 5, LEN(@PaymentRef) - 4) AS INT);
            ELSE IF @PaymentRef LIKE 'PRE_%'
            BEGIN
                -- For pre-payment orders, find the license that should be linked
                SELECT TOP 1 @RefLicenseId = Id 
                FROM Licenses 
                WHERE PaymentId = @PaymentId OR (PaymentId IS NULL AND Status = 'Pending')
                ORDER BY CreatedAt DESC;
            END
            
            IF @RefLicenseId IS NOT NULL AND @RefLicenseId > 0
            BEGIN
                -- Link payment to license if not already linked
                UPDATE Payments SET LicenseId = @RefLicenseId WHERE Id = @PaymentId;
                UPDATE Licenses SET PaymentId = @PaymentId WHERE Id = @RefLicenseId;
                
                -- Activate the license
                UPDATE Licenses 
                SET Status = 'Active', UpdatedAt = GETUTCDATE()
                WHERE Id = @RefLicenseId;
                
                -- Add history entry
                INSERT INTO LicenseHistory (LicenseId, Timestamp, Action, PerformedBy, Details)
                SELECT 
                    @RefLicenseId,
                    GETUTCDATE(),
                    'Payment Completed - License Activated',
                    l.ApplicantEmail,
                    'Payment completed with transaction ID: ' + @TransactionId + '. License automatically activated.'
                FROM Licenses l
                WHERE l.Id = @RefLicenseId;
                
                PRINT 'License ' + CAST(@RefLicenseId AS NVARCHAR(10)) + ' linked and activated after payment completion';
            END
        END
    END
END
GO

PRINT 'sp_CompletePayment updated for payment-first workflow with auto-activation';
GO
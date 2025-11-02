-- Stored Procedure: Link Payment to License
-- Updates payment and license records to establish relationship
CREATE OR ALTER PROCEDURE sp_LinkPaymentToLicense
    @PaymentId INT,
    @LicenseId INT,
    @TenantId NVARCHAR(50)
AS
BEGIN
    SET NOCOUNT ON;
    
    BEGIN TRANSACTION;
    
    BEGIN TRY
        -- Validate payment exists and belongs to tenant
        IF NOT EXISTS (SELECT 1 FROM Payments WHERE Id = @PaymentId AND TenantId = @TenantId)
        BEGIN
            ROLLBACK TRANSACTION;
            THROW 50001, 'Payment not found or access denied', 1;
        END
        
        -- Validate license exists and belongs to tenant
        IF NOT EXISTS (SELECT 1 FROM Licenses WHERE Id = @LicenseId AND TenantId = @TenantId)
        BEGIN
            ROLLBACK TRANSACTION;
            THROW 50002, 'License not found or access denied', 1;
        END
        
        -- Update payment reference to use the actual license ID
        UPDATE Payments 
        SET PaymentReference = 'LIC_' + CAST(@LicenseId AS NVARCHAR(50)),
            LicenseId = @LicenseId,
            UpdatedAt = GETUTCDATE() 
        WHERE Id = @PaymentId;
        
        -- Update license with payment ID
        UPDATE Licenses 
        SET PaymentId = @PaymentId,
            UpdatedAt = GETUTCDATE() 
        WHERE Id = @LicenseId;
        
        -- Check if payment is completed and if license was created by admin
        DECLARE @PaymentStatus NVARCHAR(50);
        DECLARE @TransactionId NVARCHAR(200);
        DECLARE @ApplicantEmail NVARCHAR(256);
        DECLARE @IsAdminCreated BIT;
        
        SELECT @PaymentStatus = Status, @TransactionId = RazorpayPaymentId 
        FROM Payments 
        WHERE Id = @PaymentId;
        
        SELECT @ApplicantEmail = ApplicantEmail, @IsAdminCreated = IsAdminCreated 
        FROM Licenses 
        WHERE Id = @LicenseId;
        
        -- If payment is completed, only activate admin-created licenses automatically
        -- User-created licenses stay Pending and require manual admin approval
        IF @PaymentStatus = 'Completed'
        BEGIN
            IF @IsAdminCreated = 1
            BEGIN
                -- Admin-created license: Auto-activate after payment
                UPDATE Licenses 
                SET Status = 'Active', UpdatedAt = GETUTCDATE()
                WHERE Id = @LicenseId;
                
                -- Add history entry
                INSERT INTO LicenseHistory (LicenseId, Timestamp, Action, PerformedBy, Details)
                VALUES (
                    @LicenseId,
                    GETUTCDATE(),
                    'Payment Linked - Admin License Activated',
                    @ApplicantEmail,
                    'Payment ' + CAST(@PaymentId AS NVARCHAR(10)) + ' linked and admin-created license automatically activated after completed payment.'
                );
                
                PRINT 'Admin-created license ' + CAST(@LicenseId AS NVARCHAR(10)) + ' activated after linking to completed payment ' + CAST(@PaymentId AS NVARCHAR(10));
            END
            ELSE
            BEGIN
                -- User-created license: Keep as Pending, requires admin approval
                -- Add history entry to show payment was completed but license needs approval
                INSERT INTO LicenseHistory (LicenseId, Timestamp, Action, PerformedBy, Details)
                VALUES (
                    @LicenseId,
                    GETUTCDATE(),
                    'Payment Linked - Awaiting Approval',
                    @ApplicantEmail,
                    'Payment ' + CAST(@PaymentId AS NVARCHAR(10)) + ' linked successfully. User-created license remains Pending awaiting admin approval.'
                );
                
                PRINT 'User-created license ' + CAST(@LicenseId AS NVARCHAR(10)) + ' linked to completed payment ' + CAST(@PaymentId AS NVARCHAR(10)) + ' but remains Pending (awaiting admin approval)';
            END
        END
        
        COMMIT TRANSACTION;
        
        SELECT 
            @PaymentId as PaymentId,
            @LicenseId as LicenseId,
            'SUCCESS' as Status,
            CASE 
                WHEN @PaymentStatus = 'Completed' AND @IsAdminCreated = 1 THEN 'Payment linked and admin license activated successfully'
                WHEN @PaymentStatus = 'Completed' AND @IsAdminCreated = 0 THEN 'Payment linked successfully. User license remains pending admin approval.'
                ELSE 'Payment linked to license successfully'
            END as Message;
            
    END TRY
    BEGIN CATCH
        ROLLBACK TRANSACTION;
        
        DECLARE @ErrorMessage NVARCHAR(4000) = ERROR_MESSAGE();
        DECLARE @ErrorSeverity INT = ERROR_SEVERITY();
        DECLARE @ErrorState INT = ERROR_STATE();
        
        RAISERROR(@ErrorMessage, @ErrorSeverity, @ErrorState);
    END CATCH
END
GO
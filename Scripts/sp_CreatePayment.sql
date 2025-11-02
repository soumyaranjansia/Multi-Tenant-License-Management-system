-- Stored Procedure: Create Payment
-- Generates unique payment number and creates payment record
CREATE OR ALTER PROCEDURE sp_CreatePayment
    @TenantId NVARCHAR(50),
    @LicenseId INT,
    @Amount DECIMAL(18,2),
    @Currency NVARCHAR(3),
    @PaymentNumber NVARCHAR(50) OUTPUT,
    @PaymentId INT OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    
    BEGIN TRANSACTION;
    
    BEGIN TRY
        -- Validate tenant exists
        IF NOT EXISTS (SELECT 1 FROM Tenants WHERE TenantId = @TenantId)
        BEGIN
            ROLLBACK TRANSACTION;
            THROW 50001, 'Invalid TenantId', 1;
        END
        
        -- Validate license exists and belongs to tenant
        IF NOT EXISTS (SELECT 1 FROM Licenses WHERE Id = @LicenseId AND TenantId = @TenantId)
        BEGIN
            ROLLBACK TRANSACTION;
            THROW 50002, 'Invalid LicenseId for tenant', 1;
        END
        
        -- Generate payment number: PAY-YYYY-NNNNNN
        DECLARE @Year NVARCHAR(4) = CAST(YEAR(GETUTCDATE()) AS NVARCHAR(4));
        DECLARE @MaxPaymentId INT;
        
        SELECT @MaxPaymentId = ISNULL(MAX(Id), 0) FROM Payments WHERE TenantId = @TenantId;
        SET @PaymentNumber = 'PAY-' + @Year + '-' + RIGHT('000000' + CAST(@MaxPaymentId + 1 AS NVARCHAR(6)), 6);
        
        -- Insert payment
        INSERT INTO Payments (TenantId, LicenseId, PaymentNumber, Amount, Currency, Status, CreatedAt)
        VALUES (@TenantId, @LicenseId, @PaymentNumber, @Amount, @Currency, 'Pending', GETUTCDATE());
        
        SET @PaymentId = SCOPE_IDENTITY();
        
        COMMIT TRANSACTION;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0
            ROLLBACK TRANSACTION;
        THROW;
    END CATCH
END
GO

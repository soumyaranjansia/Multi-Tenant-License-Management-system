-- =====================================================
-- Simplified Stored Procedures Matching Actual Schema
-- =====================================================
USE Gov2Biz;
GO

-- =====================================================
-- Stored Procedure: Create Payment with Invoice
-- =====================================================
CREATE OR ALTER PROCEDURE sp_CreatePaymentWithInvoice
    @LicenseId INT,
    @Amount DECIMAL(18,2),
    @PaymentMethod NVARCHAR(100),
    @TenantId NVARCHAR(50),
    @RazorpayOrderId NVARCHAR(100) = NULL,
    @NewPaymentId INT OUTPUT,
    @InvoiceId NVARCHAR(100) OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    
    -- Generate Invoice ID: INV-YYYYMMDD-XXXXXX
    DECLARE @DatePart NVARCHAR(8) = CONVERT(NVARCHAR(8), GETUTCDATE(), 112);
    DECLARE @RandomPart NVARCHAR(6) = RIGHT('000000' + CAST(ABS(CHECKSUM(NEWID())) % 1000000 AS NVARCHAR(6)), 6);
    SET @InvoiceId = 'INV-' + @DatePart + '-' + @RandomPart;
    
    -- Generate unique payment reference
    -- For pre-payment (LicenseId = 0), use timestamp-based reference
    -- For post-payment, use license ID
    DECLARE @PaymentRef NVARCHAR(200);
    IF @LicenseId = 0
        SET @PaymentRef = 'PRE_' + @DatePart + '-' + @RandomPart;
    ELSE
        SET @PaymentRef = 'LIC_' + CAST(@LicenseId AS NVARCHAR(50));
    
    -- Create metadata JSON string
    DECLARE @MetadataJson NVARCHAR(MAX) = '{"licenseId": ' + CAST(@LicenseId AS NVARCHAR(10)) + ', "paymentMethod": "' + @PaymentMethod + '"}';
    
    INSERT INTO Payments (
        PaymentReference,
        Amount,
        Currency,
        Status,
        Metadata,
        InvoiceId,
        RazorpayOrderId,
        TenantId,
        CreatedAt,
        UpdatedAt
    )
    VALUES (
        @PaymentRef,
        @Amount,
        'INR',
        'Pending',
        @MetadataJson,
        @InvoiceId,
        @RazorpayOrderId,
        @TenantId,
        GETUTCDATE(),
        GETUTCDATE()
    );
    
    SET @NewPaymentId = SCOPE_IDENTITY();
    
    -- Link payment to license
    UPDATE Licenses 
    SET PaymentId = @NewPaymentId, UpdatedAt = GETUTCDATE()
    WHERE Id = @LicenseId;
END
GO

-- =====================================================
-- Stored Procedure: Complete Payment
-- =====================================================
CREATE OR ALTER PROCEDURE sp_CompletePayment
    @PaymentId INT,
    @RazorpayPaymentId NVARCHAR(100),
    @RazorpaySignature NVARCHAR(500),
    @TransactionId NVARCHAR(200)
AS
BEGIN
    SET NOCOUNT ON;
    
    DECLARE @Metadata NVARCHAR(MAX);
    SELECT @Metadata = ISNULL(Metadata, '{}') FROM Payments WHERE Id = @PaymentId;
    
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
    
    -- Get associated license ID from payment reference
    DECLARE @LicenseId INT;
    DECLARE @PaymentRef NVARCHAR(200);
    
    SELECT @PaymentRef = PaymentReference FROM Payments WHERE Id = @PaymentId;
    
    -- Extract license ID from reference (format: LIC_123)
    IF @PaymentRef LIKE 'LIC_%'
    BEGIN
        SET @LicenseId = CAST(SUBSTRING(@PaymentRef, 5, LEN(@PaymentRef)) AS INT);
        
        -- Update license status to Pending (waiting for admin approval)
        UPDATE Licenses 
        SET Status = 'Pending', UpdatedAt = GETUTCDATE()
        WHERE Id = @LicenseId;
        
        -- Add history entry
        INSERT INTO LicenseHistory (LicenseId, Timestamp, Action, PerformedBy, Details)
        SELECT 
            @LicenseId,
            GETUTCDATE(),
            'Payment Completed',
            l.ApplicantEmail,
            'Payment completed with transaction ID: ' + @TransactionId + '. License awaiting approval.'
        FROM Licenses l
        WHERE l.Id = @LicenseId;
    END
END
GO

-- =====================================================
-- Stored Procedure: Get Payment Details with Invoice
-- =====================================================
CREATE OR ALTER PROCEDURE sp_GetPaymentByLicenseId
    @LicenseId INT
AS
BEGIN
    SET NOCOUNT ON;
    
    SELECT 
        p.Id,
        @LicenseId as LicenseId,
        p.Amount,
        JSON_VALUE(p.Metadata, '$.paymentMethod') as PaymentMethod,
        p.Status as PaymentStatus,
        JSON_VALUE(p.Metadata, '$.transactionId') as TransactionId,
        p.InvoiceId,
        p.RazorpayOrderId,
        p.RazorpayPaymentId,
        p.CreatedAt,
        p.UpdatedAt,
        l.ApplicantName,
        l.LicenseType,
        l.Status AS LicenseStatus
    FROM Licenses l
    LEFT JOIN Payments p ON l.PaymentId = p.Id
    WHERE l.Id = @LicenseId;
END
GO

PRINT 'All payment stored procedures created successfully!';
GO

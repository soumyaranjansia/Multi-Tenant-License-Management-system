-- =====================================================
-- Add Payment Enhancements for Razorpay Integration
-- =====================================================
USE Gov2Biz;
GO

-- Add new columns to Payments table
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[Payments]') AND name = 'InvoiceId')
BEGIN
    ALTER TABLE Payments ADD InvoiceId NVARCHAR(100) NULL;
END
GO

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[Payments]') AND name = 'RazorpayOrderId')
BEGIN
    ALTER TABLE Payments ADD RazorpayOrderId NVARCHAR(100) NULL;
END
GO

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[Payments]') AND name = 'RazorpayPaymentId')
BEGIN
    ALTER TABLE Payments ADD RazorpayPaymentId NVARCHAR(100) NULL;
END
GO

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[Payments]') AND name = 'RazorpaySignature')
BEGIN
    ALTER TABLE Payments ADD RazorpaySignature NVARCHAR(500) NULL;
END
GO

-- Update existing PaymentMethod column if it's too small
IF EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[Payments]') AND name = 'PaymentMethod' AND max_length < 100)
BEGIN
    ALTER TABLE Payments ALTER COLUMN PaymentMethod NVARCHAR(100) NULL;
END
GO

-- Add PaymentId reference to Licenses table
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[Licenses]') AND name = 'PaymentId')
BEGIN
    ALTER TABLE Licenses ADD PaymentId INT NULL;
END
GO

-- Add foreign key constraint
IF NOT EXISTS (SELECT * FROM sys.foreign_keys WHERE name = 'FK_Licenses_Payments')
BEGIN
    ALTER TABLE Licenses 
    ADD CONSTRAINT FK_Licenses_Payments 
    FOREIGN KEY (PaymentId) REFERENCES Payments(Id);
END
GO

-- Create index for better performance
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_Licenses_PaymentId')
BEGIN
    CREATE INDEX IX_Licenses_PaymentId ON Licenses(PaymentId);
END
GO

-- Create index on invoice ID for quick lookups
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_Payments_InvoiceId')
BEGIN
    CREATE INDEX IX_Payments_InvoiceId ON Payments(InvoiceId);
END
GO

-- Create index on Razorpay Order ID
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_Payments_RazorpayOrderId')
BEGIN
    CREATE INDEX IX_Payments_RazorpayOrderId ON Payments(RazorpayOrderId);
END
GO

PRINT 'Payment enhancements added successfully!';
GO

-- =====================================================
-- Stored Procedure: Create Payment with Invoice
-- =====================================================
CREATE OR ALTER PROCEDURE sp_CreatePaymentWithInvoice
    @LicenseId INT,
    @Amount DECIMAL(18,2),
    @PaymentMethod NVARCHAR(100),
    @TenantId INT,
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
    
    INSERT INTO Payments (
        LicenseId, 
        Amount, 
        PaymentMethod, 
        PaymentStatus, 
        InvoiceId,
        RazorpayOrderId,
        TenantId, 
        CreatedAt, 
        UpdatedAt
    )
    VALUES (
        @LicenseId, 
        @Amount, 
        @PaymentMethod, 
        'Pending',
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
    
    -- Update payment status
    UPDATE Payments 
    SET 
        PaymentStatus = 'Completed',
        RazorpayPaymentId = @RazorpayPaymentId,
        RazorpaySignature = @RazorpaySignature,
        TransactionId = @TransactionId,
        UpdatedAt = GETUTCDATE()
    WHERE Id = @PaymentId;
    
    -- Get associated license
    DECLARE @LicenseId INT;
    SELECT @LicenseId = LicenseId FROM Payments WHERE Id = @PaymentId;
    
    -- Update license status to Pending (waiting for admin approval)
    UPDATE Licenses 
    SET Status = 'Pending', UpdatedAt = GETUTCDATE()
    WHERE Id = @LicenseId;
    
    -- Add history entry
    DECLARE @UserId INT;
    SELECT @UserId = UserId FROM Licenses WHERE Id = @LicenseId;
    
    INSERT INTO LicenseHistory (LicenseId, OldStatus, NewStatus, ChangedBy, Comments, CreatedAt)
    VALUES (@LicenseId, 'Draft', 'Pending', @UserId, 'Payment completed, awaiting approval', GETUTCDATE());
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
        p.LicenseId,
        p.Amount,
        p.PaymentMethod,
        p.PaymentStatus,
        p.TransactionId,
        p.InvoiceId,
        p.RazorpayOrderId,
        p.RazorpayPaymentId,
        p.CreatedAt,
        p.UpdatedAt,
        l.ApplicantName,
        l.LicenseType,
        l.Status AS LicenseStatus
    FROM Payments p
    INNER JOIN Licenses l ON p.LicenseId = l.Id
    WHERE p.LicenseId = @LicenseId;
END
GO

PRINT 'Payment stored procedures created successfully!';
GO

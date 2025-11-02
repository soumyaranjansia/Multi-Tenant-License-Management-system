-- =====================================================  
-- SECTION 3: STORED PROCEDURES
-- =====================================================

-- 1. License Creation Procedure (Enhanced with IsAdminCreated)
CREATE OR ALTER PROCEDURE sp_CreateLicense
    @ApplicantName NVARCHAR(256),
    @ApplicantEmail NVARCHAR(256),
    @LicenseType NVARCHAR(100),
    @ExpiryDate DATETIME2,
    @TenantId NVARCHAR(50),
    @Metadata NVARCHAR(MAX) = NULL,
    @Amount DECIMAL(18,2) = 100.00,
    @IsAdminCreated BIT = 0
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    BEGIN TRY
        BEGIN TRANSACTION;

        -- Validate tenant exists
        IF NOT EXISTS (SELECT 1 FROM Tenants WHERE TenantId = @TenantId AND IsActive = 1)
        BEGIN
            RAISERROR('Invalid or inactive tenant', 16, 1);
            RETURN;
        END

        DECLARE @NewId INT;
        DECLARE @LicenseNumber NVARCHAR(100);
        DECLARE @Year NVARCHAR(4) = FORMAT(GETUTCDATE(), 'yyyy');
        
        -- Generate unique license number: LIC-YYYY-NNNNNN
        DECLARE @NextSeq INT = ISNULL((SELECT MAX(Id) FROM Licenses), 0) + 1;
        SET @LicenseNumber = CONCAT('LIC-', @Year, '-', RIGHT('000000' + CAST(@NextSeq AS NVARCHAR(6)), 6));

        -- Insert license
        INSERT INTO Licenses (
            LicenseNumber, ApplicantName, ApplicantEmail, LicenseType, 
            Status, ExpiryDate, Amount, Metadata, TenantId, IsAdminCreated,
            CreatedAt, UpdatedAt
        )
        VALUES (
            @LicenseNumber, @ApplicantName, @ApplicantEmail, @LicenseType,
            'Pending', @ExpiryDate, @Amount, @Metadata, @TenantId, @IsAdminCreated,
            SYSUTCDATETIME(), SYSUTCDATETIME()
        );

        SET @NewId = SCOPE_IDENTITY();

        -- Add history entry
        INSERT INTO LicenseHistory (LicenseId, Action, PerformedBy, Details)
        VALUES (@NewId, 'Created', @ApplicantEmail, CONCAT('License created for ', @ApplicantName));

        COMMIT TRANSACTION;

        -- Return new ID and license number
        SELECT @NewId AS NewId, @LicenseNumber AS LicenseNumber;

    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0
            ROLLBACK TRANSACTION;
        
        DECLARE @ErrorMessage NVARCHAR(4000) = ERROR_MESSAGE();
        DECLARE @ErrorSeverity INT = ERROR_SEVERITY();
        DECLARE @ErrorState INT = ERROR_STATE();
        
        RAISERROR(@ErrorMessage, @ErrorSeverity, @ErrorState);
    END CATCH
END
GO

-- 2. Payment Linking Procedure (Enhanced with Admin License Auto-Activation)
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
        DECLARE @ApplicantEmail NVARCHAR(256);
        DECLARE @IsAdminCreated BIT;
        
        SELECT @PaymentStatus = Status 
        FROM Payments 
        WHERE Id = @PaymentId;
        
        SELECT @ApplicantEmail = ApplicantEmail, @IsAdminCreated = IsAdminCreated 
        FROM Licenses 
        WHERE Id = @LicenseId;
        
        -- If payment is completed, only activate admin-created licenses automatically
        IF @PaymentStatus = 'Completed'
        BEGIN
            IF @IsAdminCreated = 1
            BEGIN
                -- Admin-created license: Auto-activate after payment
                UPDATE Licenses 
                SET Status = 'Active', UpdatedAt = GETUTCDATE()
                WHERE Id = @LicenseId;
                
                INSERT INTO LicenseHistory (LicenseId, Timestamp, Action, PerformedBy, Details)
                VALUES (
                    @LicenseId, GETUTCDATE(), 'Payment Linked - Admin License Activated',
                    @ApplicantEmail, 'Payment ' + CAST(@PaymentId AS NVARCHAR(10)) + ' linked and admin-created license automatically activated.'
                );
                
                PRINT 'Admin-created license ' + CAST(@LicenseId AS NVARCHAR(10)) + ' activated after payment';
            END
            ELSE
            BEGIN
                -- User-created license: Keep as Pending, requires admin approval
                INSERT INTO LicenseHistory (LicenseId, Timestamp, Action, PerformedBy, Details)
                VALUES (
                    @LicenseId, GETUTCDATE(), 'Payment Linked - Awaiting Approval',
                    @ApplicantEmail, 'Payment ' + CAST(@PaymentId AS NVARCHAR(10)) + ' linked successfully. User-created license remains Pending awaiting admin approval.'
                );
                
                PRINT 'User-created license ' + CAST(@LicenseId AS NVARCHAR(10)) + ' remains Pending (awaiting admin approval)';
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
        IF @@TRANCOUNT > 0
            ROLLBACK TRANSACTION;
        THROW;
    END CATCH
END
GO

-- 3. Payment Completion Procedure
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
    
    -- License activation is handled by sp_LinkPaymentToLicense
    PRINT 'Payment ' + CAST(@PaymentId AS NVARCHAR(10)) + ' completed. License activation handled by linking procedure.';
END
GO

-- 4. License Status Update Procedure  
CREATE OR ALTER PROCEDURE sp_UpdateLicenseStatus
    @LicenseId INT,
    @Status NVARCHAR(50),
    @TenantId NVARCHAR(50) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    
    DECLARE @sql NVARCHAR(MAX) = 'UPDATE Licenses SET Status = @Status, UpdatedAt = GETUTCDATE() WHERE Id = @LicenseId';
    
    -- Add tenant filter if provided for additional security
    IF @TenantId IS NOT NULL
    BEGIN
        SET @sql = @sql + ' AND TenantId = @TenantId';
    END
    
    EXEC sp_executesql @sql, N'@LicenseId INT, @Status NVARCHAR(50), @TenantId NVARCHAR(50)', @LicenseId, @Status, @TenantId;
    
    SELECT @@ROWCOUNT AS RowsAffected;
END
GO

-- 5. Get Licenses by Tenant
CREATE OR ALTER PROCEDURE sp_GetLicensesByTenant
    @TenantId NVARCHAR(50),
    @Status NVARCHAR(50) = NULL,
    @ExpiringInDays INT = NULL
AS
BEGIN
    SET NOCOUNT ON;
    
    SELECT 
        Id, LicenseNumber, ApplicantName, ApplicantEmail,
        LicenseType, Status, ExpiryDate, Amount,
        CreatedAt, UpdatedAt, IsAdminCreated
    FROM Licenses
    WHERE TenantId = @TenantId
    AND (@Status IS NULL OR Status = @Status)
    AND (@ExpiringInDays IS NULL OR ExpiryDate <= DATEADD(DAY, @ExpiringInDays, GETUTCDATE()))
    ORDER BY CreatedAt DESC;
END
GO

-- 6. Get Active License Types
CREATE OR ALTER PROCEDURE sp_GetActiveLicenseTypes
AS
BEGIN
    SET NOCOUNT ON;
    
    SELECT Id, Name, Description, Price, ValidityMonths, IsActive
    FROM LicenseTypes
    WHERE IsActive = 1
    ORDER BY Name;
END
GO

-- 7. Get Tenant Users (Admin functionality)
CREATE OR ALTER PROCEDURE sp_GetTenantUsers
    @TenantId NVARCHAR(50)
AS
BEGIN
    SET NOCOUNT ON;
    
    SELECT Id, Username, Email, Roles, IsActive, CreatedAt
    FROM Users
    WHERE TenantId = @TenantId AND IsActive = 1
    ORDER BY Username;
END
GO

PRINT 'All stored procedures created successfully';
GO
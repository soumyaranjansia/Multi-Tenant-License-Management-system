-- Add Documents table to existing schema
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[Documents]') AND type in (N'U'))
BEGIN
    CREATE TABLE Documents (
        Id INT IDENTITY(1,1) PRIMARY KEY,
        TenantId NVARCHAR(50) NOT NULL,
        LicenseId INT NOT NULL,
        FileName NVARCHAR(255) NOT NULL,
        StoredFileName NVARCHAR(255) NOT NULL,
        ContentType NVARCHAR(100) NOT NULL,
        FileSizeBytes BIGINT NOT NULL,
        UploadedAt DATETIME NOT NULL DEFAULT GETUTCDATE(),
        UploadedBy NVARCHAR(100) NOT NULL,
        CONSTRAINT FK_Documents_Licenses FOREIGN KEY (LicenseId) REFERENCES Licenses(Id),
        INDEX IX_Documents_TenantLicense (TenantId, LicenseId)
    );
    
    PRINT 'Documents table created successfully';
END
ELSE
BEGIN
    PRINT 'Documents table already exists';
END
GO

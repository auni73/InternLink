-- 006_admin_rejection_reason.sql
-- Description: Add AdminRejectionReason column to Companies table for moderation feedback

USE [InternLink];
GO

IF EXISTS (SELECT 1 FROM dbo.SchemaVersions WHERE ScriptName = N'006_admin_rejection_reason.sql')
BEGIN
    PRINT '006_admin_rejection_reason.sql already applied. Skipping.';
    RETURN;
END
GO

IF NOT EXISTS (
    SELECT 1 
    FROM sys.columns 
    WHERE object_id = OBJECT_ID(N'dbo.Companies') 
      AND name = N'AdminRejectionReason'
)
BEGIN
    ALTER TABLE dbo.Companies
    ADD AdminRejectionReason NVARCHAR(500) NULL;
END
GO

IF NOT EXISTS (SELECT 1 FROM dbo.SchemaVersions WHERE ScriptName = N'006_admin_rejection_reason.sql')
BEGIN
    INSERT INTO dbo.SchemaVersions (ScriptName) VALUES (N'006_admin_rejection_reason.sql');
END
GO

-- 010_data_protection_keys.sql
-- Persists ASP.NET Core Data Protection keys to SQL so they survive container
-- restarts and re-deployments on Render (ephemeral filesystem).
-- Table schema matches Microsoft.AspNetCore.DataProtection.EntityFrameworkCore.

IF NOT EXISTS (
    SELECT 1
    FROM sys.tables
    WHERE name = N'DataProtectionKeys'
      AND schema_id = SCHEMA_ID(N'dbo')
)
BEGIN
    CREATE TABLE dbo.DataProtectionKeys (
        Id            INT              NOT NULL IDENTITY(1,1),
        FriendlyName  NVARCHAR(MAX)    NULL,
        Xml           NVARCHAR(MAX)    NULL,
        CONSTRAINT PK_DataProtectionKeys PRIMARY KEY CLUSTERED (Id)
    );
END

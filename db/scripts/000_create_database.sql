-- 000_create_database.sql
-- Description: Create InternLink database if missing and create SchemaVersions tracking table

IF NOT EXISTS (SELECT 1 FROM sys.databases WHERE name = N'InternLink')
BEGIN
    CREATE DATABASE [InternLink];
END
GO

USE [InternLink];
GO

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = N'SchemaVersions' AND schema_id = SCHEMA_ID(N'dbo'))
BEGIN
    CREATE TABLE dbo.SchemaVersions (
        ScriptName NVARCHAR(200) NOT NULL,
        AppliedAt DATETIMEOFFSET NOT NULL CONSTRAINT DF_SchemaVersions_AppliedAt DEFAULT SYSDATETIMEOFFSET(),
        CONSTRAINT PK_SchemaVersions PRIMARY KEY CLUSTERED (ScriptName)
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM dbo.SchemaVersions WHERE ScriptName = N'000_create_database.sql')
BEGIN
    INSERT INTO dbo.SchemaVersions (ScriptName) VALUES (N'000_create_database.sql');
END
GO

-- 008_external_jobs.sql
-- Description: Supports external and aggregated job postings (Arbeitnow, BDJobs, circulars)
--              Makes CompanyId nullable, adds external metadata columns and partial unique index.

USE [InternLink];
GO

IF EXISTS (SELECT 1 FROM dbo.SchemaVersions WHERE ScriptName = N'008_external_jobs.sql')
BEGIN
    PRINT '008_external_jobs.sql already applied. Skipping.';
    RETURN;
END
GO

-- 1. Make CompanyId NULLABLE in dbo.Jobs to support external postings
IF EXISTS (
    SELECT 1 
    FROM sys.columns 
    WHERE object_id = OBJECT_ID(N'dbo.Jobs') 
      AND name = N'CompanyId' 
      AND is_nullable = 0
)
BEGIN
    -- Drop existing FK constraint if present
    IF EXISTS (
        SELECT 1 
        FROM sys.foreign_keys 
        WHERE name = N'FK_Jobs_Companies_CompanyId' 
          AND parent_object_id = OBJECT_ID(N'dbo.Jobs')
    )
    BEGIN
        ALTER TABLE dbo.Jobs DROP CONSTRAINT FK_Jobs_Companies_CompanyId;
    END

    -- Alter column to allow NULL
    ALTER TABLE dbo.Jobs ALTER COLUMN CompanyId UNIQUEIDENTIFIER NULL;

    -- Re-add FK with ON DELETE SET NULL
    ALTER TABLE dbo.Jobs ADD CONSTRAINT FK_Jobs_Companies_CompanyId
        FOREIGN KEY (CompanyId) REFERENCES dbo.Companies (Id) ON DELETE SET NULL;

    PRINT 'Altered dbo.Jobs.CompanyId to NULLABLE with ON DELETE SET NULL.';
END
GO

-- 2. Add Source column (0: Internal, 1: External)
IF NOT EXISTS (
    SELECT 1 
    FROM sys.columns 
    WHERE object_id = OBJECT_ID(N'dbo.Jobs') 
      AND name = N'Source'
)
BEGIN
    ALTER TABLE dbo.Jobs 
        ADD Source TINYINT NOT NULL 
            CONSTRAINT DF_Jobs_Source DEFAULT 0
            CONSTRAINT CK_Jobs_Source CHECK (Source IN (0, 1));

    PRINT 'Added Source column to dbo.Jobs.';
END
GO

-- 3. Add External Metadata Columns
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'dbo.Jobs') AND name = N'ExternalSourceName')
BEGIN
    ALTER TABLE dbo.Jobs ADD ExternalSourceName NVARCHAR(100) NULL;
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'dbo.Jobs') AND name = N'ExternalJobId')
BEGIN
    ALTER TABLE dbo.Jobs ADD ExternalJobId NVARCHAR(200) NULL;
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'dbo.Jobs') AND name = N'ExternalApplyUrl')
BEGIN
    ALTER TABLE dbo.Jobs ADD ExternalApplyUrl NVARCHAR(1000) NULL;
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'dbo.Jobs') AND name = N'CompanyNameSnapshot')
BEGIN
    ALTER TABLE dbo.Jobs ADD CompanyNameSnapshot NVARCHAR(200) NULL;
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'dbo.Jobs') AND name = N'LastSyncedAt')
BEGIN
    ALTER TABLE dbo.Jobs ADD LastSyncedAt DATETIMEOFFSET NULL;
END
GO

-- 4. Create Partial Unique Index for Deduplication
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Jobs_ExternalSourceName_ExternalJobId' AND object_id = OBJECT_ID(N'dbo.Jobs'))
BEGIN
    CREATE UNIQUE NONCLUSTERED INDEX IX_Jobs_ExternalSourceName_ExternalJobId
        ON dbo.Jobs (ExternalSourceName, ExternalJobId)
        WHERE ExternalSourceName IS NOT NULL AND ExternalJobId IS NOT NULL;

    PRINT 'Created unique filtered index IX_Jobs_ExternalSourceName_ExternalJobId.';
END
GO

-- 5. Create Index on Source for Fast Internal/External Filtering
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Jobs_Source' AND object_id = OBJECT_ID(N'dbo.Jobs'))
BEGIN
    CREATE NONCLUSTERED INDEX IX_Jobs_Source
        ON dbo.Jobs (Source)
        INCLUDE (IsApproved, IsClosed, DeadLine);

    PRINT 'Created index IX_Jobs_Source.';
END
GO

-- 6. Record in SchemaVersions ledger
IF NOT EXISTS (SELECT 1 FROM dbo.SchemaVersions WHERE ScriptName = N'008_external_jobs.sql')
BEGIN
    INSERT INTO dbo.SchemaVersions (ScriptName) VALUES (N'008_external_jobs.sql');
    PRINT 'Recorded 008_external_jobs.sql in SchemaVersions.';
END
GO

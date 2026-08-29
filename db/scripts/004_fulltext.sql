-- 004_fulltext.sql
-- Description: Full-Text Search Catalog and Index on Jobs (Title, CoreDescription)

USE [InternLink];
GO

IF EXISTS (SELECT 1 FROM dbo.SchemaVersions WHERE ScriptName = N'004_fulltext.sql')
BEGIN
    PRINT '004_fulltext.sql already applied. Skipping.';
    RETURN;
END
GO

IF (CAST(SERVERPROPERTY('IsFullTextInstalled') AS INT) = 1)
BEGIN
    PRINT 'Full-Text Search component detected. Creating Full-Text Catalog and Index...';

    -- 1. Create Full-Text Catalog if not exists (using dynamic SQL for safe compilation on non-FTS instances)
    IF NOT EXISTS (SELECT 1 FROM sys.fulltext_catalogs WHERE name = N'InternLinkCatalog')
    BEGIN
        EXEC(N'CREATE FULLTEXT CATALOG InternLinkCatalog AS DEFAULT;');
        PRINT 'Created full-text catalog: InternLinkCatalog';
    END

    -- 2. Create Full-Text Index on dbo.Jobs
    IF NOT EXISTS (SELECT 1 FROM sys.fulltext_indexes WHERE object_id = OBJECT_ID(N'dbo.Jobs'))
    BEGIN
        EXEC(N'CREATE FULLTEXT INDEX ON dbo.Jobs
        (
            Title LANGUAGE 1033,
            CoreDescription LANGUAGE 1033
        )
        KEY INDEX PK_Jobs ON InternLinkCatalog
        WITH CHANGE_TRACKING AUTO;');

        PRINT 'Created full-text index on dbo.Jobs (Title, CoreDescription)';
    END
END
ELSE
BEGIN
    PRINT '****************************************************************************************';
    PRINT 'WARNING: Full-Text Search is NOT installed on this SQL Server instance.';
    PRINT 'Skipping full-text catalog/index creation. The application will use SQL LIKE fallback.';
    PRINT '****************************************************************************************';
END
GO

IF NOT EXISTS (SELECT 1 FROM dbo.SchemaVersions WHERE ScriptName = N'004_fulltext.sql')
BEGIN
    INSERT INTO dbo.SchemaVersions (ScriptName) VALUES (N'004_fulltext.sql');
END
GO

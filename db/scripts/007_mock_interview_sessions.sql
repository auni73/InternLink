-- 007_mock_interview_sessions.sql
-- Description: MockInterviewSessions table backing the persistent AI mock interview chatbot

USE [InternLink];
GO

IF EXISTS (SELECT 1 FROM dbo.SchemaVersions WHERE ScriptName = N'007_mock_interview_sessions.sql')
BEGIN
    PRINT '007_mock_interview_sessions.sql already applied. Skipping.';
    RETURN;
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = N'MockInterviewSessions' AND schema_id = SCHEMA_ID(N'dbo'))
BEGIN
    CREATE TABLE dbo.MockInterviewSessions (
        Id UNIQUEIDENTIFIER NOT NULL CONSTRAINT DF_MockInterviewSessions_Id DEFAULT NEWSEQUENTIALID(),
        StudentId UNIQUEIDENTIFIER NOT NULL,
        Role NVARCHAR(100) NOT NULL,
        JobId UNIQUEIDENTIFIER NULL,
        TranscriptJson NVARCHAR(MAX) NOT NULL,
        Status TINYINT NOT NULL CONSTRAINT DF_MockInterviewSessions_Status DEFAULT 0,
        ReportJson NVARCHAR(MAX) NULL,
        CreatedAt DATETIMEOFFSET NOT NULL CONSTRAINT DF_MockInterviewSessions_CreatedAt DEFAULT SYSDATETIMEOFFSET(),
        CompletedAt DATETIMEOFFSET NULL,
        CONSTRAINT PK_MockInterviewSessions PRIMARY KEY CLUSTERED (Id),
        CONSTRAINT CK_MockInterviewSessions_Status CHECK (Status IN (0, 1)), -- 0:InProgress, 1:Completed
        CONSTRAINT CK_MockInterviewSessions_TranscriptJson CHECK (ISJSON(TranscriptJson) = 1),
        CONSTRAINT CK_MockInterviewSessions_ReportJson CHECK (ReportJson IS NULL OR ISJSON(ReportJson) = 1),
        -- Deleting the student takes their practice sessions with them. The job link stays NO ACTION
        -- because Students and Jobs both cascade from AspNetUsers, and SQL Server rejects the second
        -- cascade path. Jobs are retired with IsClosed rather than deleted, so nothing is orphaned.
        CONSTRAINT FK_MockInterviewSessions_Students_StudentId FOREIGN KEY (StudentId)
            REFERENCES dbo.Students (Id) ON DELETE CASCADE,
        CONSTRAINT FK_MockInterviewSessions_Jobs_JobId FOREIGN KEY (JobId)
            REFERENCES dbo.Jobs (Id) ON DELETE NO ACTION
    );
END
GO

-- Drives the session list: a student's sessions, newest first.
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_MockInterviewSessions_StudentId_CreatedAt')
BEGIN
    CREATE NONCLUSTERED INDEX IX_MockInterviewSessions_StudentId_CreatedAt
        ON dbo.MockInterviewSessions (StudentId, CreatedAt DESC)
        INCLUDE (Role, Status, CompletedAt);
END
GO

-- Batches after a failure still run, so the ledger row is gated on the table actually existing.
-- Otherwise a botched run marks itself applied and the next run skips the repair.
IF EXISTS (SELECT 1 FROM sys.tables WHERE name = N'MockInterviewSessions' AND schema_id = SCHEMA_ID(N'dbo'))
   AND NOT EXISTS (SELECT 1 FROM dbo.SchemaVersions WHERE ScriptName = N'007_mock_interview_sessions.sql')
BEGIN
    INSERT INTO dbo.SchemaVersions (ScriptName) VALUES (N'007_mock_interview_sessions.sql');
END
GO

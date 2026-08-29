-- 003_indexes.sql
-- Description: Performance query-pattern indexes for browse, dashboard, and lookup optimizations

USE [InternLink];
GO

IF EXISTS (SELECT 1 FROM dbo.SchemaVersions WHERE ScriptName = N'003_indexes.sql')
BEGIN
    PRINT '003_indexes.sql already applied. Skipping.';
    RETURN;
END
GO

-- 1. Jobs: Composite index for student job browsing (IsApproved, IsClosed, DeadLine)
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Jobs_IsApproved_IsClosed_DeadLine' AND object_id = OBJECT_ID(N'dbo.Jobs'))
BEGIN
    CREATE NONCLUSTERED INDEX IX_Jobs_IsApproved_IsClosed_DeadLine 
    ON dbo.Jobs (IsApproved, IsClosed, DeadLine)
    INCLUDE (Title, CompanyId, LocationType);
END
GO

-- 2. Jobs: Company jobs lookup
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Jobs_CompanyId' AND object_id = OBJECT_ID(N'dbo.Jobs'))
BEGIN
    CREATE NONCLUSTERED INDEX IX_Jobs_CompanyId 
    ON dbo.Jobs (CompanyId);
END
GO

-- 3. Applications: Student applications list
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Applications_StudentId' AND object_id = OBJECT_ID(N'dbo.Applications'))
BEGIN
    CREATE NONCLUSTERED INDEX IX_Applications_StudentId 
    ON dbo.Applications (StudentId)
    INCLUDE (JobId, ApplicationStatus, SubmittedAt);
END
GO

-- 4. Applications: Status filtering / ATS pipeline queries
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Applications_ApplicationStatus' AND object_id = OBJECT_ID(N'dbo.Applications'))
BEGIN
    CREATE NONCLUSTERED INDEX IX_Applications_ApplicationStatus 
    ON dbo.Applications (ApplicationStatus);
END
GO

-- 5. Notifications: User unread/recent notifications polling
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Notifications_TargetUserId_IsRead' AND object_id = OBJECT_ID(N'dbo.Notifications'))
BEGIN
    CREATE NONCLUSTERED INDEX IX_Notifications_TargetUserId_IsRead 
    ON dbo.Notifications (TargetUserId, IsRead)
    INCLUDE (TimeTriggered, TextPayload, EventRoutingUrl);
END
GO

-- 6. Resumes: Student resumes list
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Resumes_StudentId' AND object_id = OBJECT_ID(N'dbo.Resumes'))
BEGIN
    CREATE NONCLUSTERED INDEX IX_Resumes_StudentId 
    ON dbo.Resumes (StudentId);
END
GO

-- 7. AIHistory: User AI audit and token accounting
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_AIHistory_UserId_CreatedAt' AND object_id = OBJECT_ID(N'dbo.AIHistory'))
BEGIN
    CREATE NONCLUSTERED INDEX IX_AIHistory_UserId_CreatedAt 
    ON dbo.AIHistory (UserId, CreatedAt);
END
GO

-- 8. OtpCodes: Active OTP lookup
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_OtpCodes_UserId_ExpiresAt' AND object_id = OBJECT_ID(N'dbo.OtpCodes'))
BEGIN
    CREATE NONCLUSTERED INDEX IX_OtpCodes_UserId_ExpiresAt 
    ON dbo.OtpCodes (UserId, ExpiresAt)
    INCLUDE (CodeHash, ConsumedAt);
END
GO

IF NOT EXISTS (SELECT 1 FROM dbo.SchemaVersions WHERE ScriptName = N'003_indexes.sql')
BEGIN
    INSERT INTO dbo.SchemaVersions (ScriptName) VALUES (N'003_indexes.sql');
END
GO

-- 002_domain_tables.sql
-- Description: Core domain tables with constraints, default NEWSEQUENTIALID(), and explicit cascade rules

USE [InternLink];
GO

IF EXISTS (SELECT 1 FROM dbo.SchemaVersions WHERE ScriptName = N'002_domain_tables.sql')
BEGIN
    PRINT '002_domain_tables.sql already applied. Skipping.';
    RETURN;
END
GO

-- 1. Students
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = N'Students' AND schema_id = SCHEMA_ID(N'dbo'))
BEGIN
    CREATE TABLE dbo.Students (
        Id UNIQUEIDENTIFIER NOT NULL CONSTRAINT DF_Students_Id DEFAULT NEWSEQUENTIALID(),
        UserId UNIQUEIDENTIFIER NOT NULL,
        FirstName NVARCHAR(100) NOT NULL,
        LastName NVARCHAR(100) NOT NULL,
        CGPA DECIMAL(3,2) NOT NULL,
        InstitutionalId NVARCHAR(50) NOT NULL,
        Department NVARCHAR(100) NOT NULL,
        Biography NVARCHAR(2000) NULL,
        Interests NVARCHAR(500) NULL,
        CreatedAt DATETIMEOFFSET NOT NULL CONSTRAINT DF_Students_CreatedAt DEFAULT SYSDATETIMEOFFSET(),
        CONSTRAINT PK_Students PRIMARY KEY CLUSTERED (Id),
        CONSTRAINT UQ_Students_UserId UNIQUE (UserId),
        CONSTRAINT UQ_Students_InstitutionalId UNIQUE (InstitutionalId),
        CONSTRAINT CK_Students_CGPA CHECK (CGPA BETWEEN 0.00 AND 4.00),
        CONSTRAINT FK_Students_AspNetUsers_UserId FOREIGN KEY (UserId) 
            REFERENCES dbo.AspNetUsers (Id) ON DELETE CASCADE
    );
END
GO

-- 2. Companies
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = N'Companies' AND schema_id = SCHEMA_ID(N'dbo'))
BEGIN
    CREATE TABLE dbo.Companies (
        Id UNIQUEIDENTIFIER NOT NULL CONSTRAINT DF_Companies_Id DEFAULT NEWSEQUENTIALID(),
        UserId UNIQUEIDENTIFIER NOT NULL,
        CompanyName NVARCHAR(200) NOT NULL,
        CorporateWebsite NVARCHAR(500) NULL,
        IndustrySector NVARCHAR(100) NOT NULL,
        VerificationStatus TINYINT NOT NULL CONSTRAINT DF_Companies_VerificationStatus DEFAULT 0,
        CreatedAt DATETIMEOFFSET NOT NULL CONSTRAINT DF_Companies_CreatedAt DEFAULT SYSDATETIMEOFFSET(),
        CONSTRAINT PK_Companies PRIMARY KEY CLUSTERED (Id),
        CONSTRAINT UQ_Companies_UserId UNIQUE (UserId),
        CONSTRAINT CK_Companies_VerificationStatus CHECK (VerificationStatus IN (0, 1, 2)), -- 0:Pending, 1:Verified, 2:Rejected
        CONSTRAINT FK_Companies_AspNetUsers_UserId FOREIGN KEY (UserId) 
            REFERENCES dbo.AspNetUsers (Id) ON DELETE CASCADE
    );
END
GO

-- 3. Jobs
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = N'Jobs' AND schema_id = SCHEMA_ID(N'dbo'))
BEGIN
    CREATE TABLE dbo.Jobs (
        Id UNIQUEIDENTIFIER NOT NULL CONSTRAINT DF_Jobs_Id DEFAULT NEWSEQUENTIALID(),
        CompanyId UNIQUEIDENTIFIER NOT NULL,
        Title NVARCHAR(200) NOT NULL,
        CoreDescription NVARCHAR(MAX) NOT NULL,
        SelectionCriteria NVARCHAR(MAX) NOT NULL,
        LocationType TINYINT NOT NULL,
        DeadLine DATETIMEOFFSET NOT NULL,
        IsApproved BIT NOT NULL CONSTRAINT DF_Jobs_IsApproved DEFAULT 0,
        IsClosed BIT NOT NULL CONSTRAINT DF_Jobs_IsClosed DEFAULT 0,
        CreatedAt DATETIMEOFFSET NOT NULL CONSTRAINT DF_Jobs_CreatedAt DEFAULT SYSDATETIMEOFFSET(),
        CONSTRAINT PK_Jobs PRIMARY KEY CLUSTERED (Id), -- Explicit PK name required for FTS index reference
        CONSTRAINT CK_Jobs_LocationType CHECK (LocationType IN (0, 1, 2)), -- 0:Remote, 1:OnSite, 2:Hybrid
        CONSTRAINT FK_Jobs_Companies_CompanyId FOREIGN KEY (CompanyId) 
            REFERENCES dbo.Companies (Id) ON DELETE CASCADE
    );
END
GO

-- 4. Resumes
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = N'Resumes' AND schema_id = SCHEMA_ID(N'dbo'))
BEGIN
    CREATE TABLE dbo.Resumes (
        Id UNIQUEIDENTIFIER NOT NULL CONSTRAINT DF_Resumes_Id DEFAULT NEWSEQUENTIALID(),
        StudentId UNIQUEIDENTIFIER NOT NULL,
        DocumentPath NVARCHAR(500) NULL,
        DynamicJsonData NVARCHAR(MAX) NOT NULL CONSTRAINT DF_Resumes_DynamicJsonData DEFAULT '{}',
        LastModified DATETIMEOFFSET NOT NULL CONSTRAINT DF_Resumes_LastModified DEFAULT SYSDATETIMEOFFSET(),
        CONSTRAINT PK_Resumes PRIMARY KEY CLUSTERED (Id),
        CONSTRAINT CK_Resumes_DynamicJsonData CHECK (ISJSON(DynamicJsonData) = 1),
        CONSTRAINT FK_Resumes_Students_StudentId FOREIGN KEY (StudentId) 
            REFERENCES dbo.Students (Id) ON DELETE CASCADE
    );
END
GO

-- 5. Applications
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = N'Applications' AND schema_id = SCHEMA_ID(N'dbo'))
BEGIN
    CREATE TABLE dbo.Applications (
        Id UNIQUEIDENTIFIER NOT NULL CONSTRAINT DF_Applications_Id DEFAULT NEWSEQUENTIALID(),
        JobId UNIQUEIDENTIFIER NOT NULL,
        StudentId UNIQUEIDENTIFIER NOT NULL,
        SubmittedAt DATETIMEOFFSET NOT NULL CONSTRAINT DF_Applications_SubmittedAt DEFAULT SYSDATETIMEOFFSET(),
        ApplicationStatus TINYINT NOT NULL CONSTRAINT DF_Applications_ApplicationStatus DEFAULT 0,
        AttachedResumeId UNIQUEIDENTIFIER NULL,
        CoverLetterText NVARCHAR(MAX) NULL,
        CONSTRAINT PK_Applications PRIMARY KEY CLUSTERED (Id),
        CONSTRAINT UQ_Applications_JobId_StudentId UNIQUE (JobId, StudentId),
        CONSTRAINT CK_Applications_ApplicationStatus CHECK (ApplicationStatus IN (0, 1, 2, 3, 4)), -- 0:Applied, 1:Screened, 2:Scheduled, 3:Offered, 4:Rejected
        -- Deliberate NO ACTION on Job delete: jobs are closed, never deleted; preserves student application history
        CONSTRAINT FK_Applications_Jobs_JobId FOREIGN KEY (JobId) 
            REFERENCES dbo.Jobs (Id) ON DELETE NO ACTION,
        CONSTRAINT FK_Applications_Students_StudentId FOREIGN KEY (StudentId) 
            REFERENCES dbo.Students (Id) ON DELETE CASCADE,
        CONSTRAINT FK_Applications_Resumes_AttachedResumeId FOREIGN KEY (AttachedResumeId) 
            REFERENCES dbo.Resumes (Id) ON DELETE NO ACTION
    );
END
GO

-- 6. Interviews
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = N'Interviews' AND schema_id = SCHEMA_ID(N'dbo'))
BEGIN
    CREATE TABLE dbo.Interviews (
        Id UNIQUEIDENTIFIER NOT NULL CONSTRAINT DF_Interviews_Id DEFAULT NEWSEQUENTIALID(),
        ApplicationId UNIQUEIDENTIFIER NOT NULL,
        ScheduledDateTime DATETIMEOFFSET NOT NULL,
        ContextMeetingLink NVARCHAR(500) NOT NULL,
        StatusIndicator TINYINT NOT NULL CONSTRAINT DF_Interviews_StatusIndicator DEFAULT 0,
        CreatedAt DATETIMEOFFSET NOT NULL CONSTRAINT DF_Interviews_CreatedAt DEFAULT SYSDATETIMEOFFSET(),
        CONSTRAINT PK_Interviews PRIMARY KEY CLUSTERED (Id),
        CONSTRAINT CK_Interviews_StatusIndicator CHECK (StatusIndicator IN (0, 1, 2)), -- 0:Scheduled, 1:Completed, 2:Cancelled
        CONSTRAINT FK_Interviews_Applications_ApplicationId FOREIGN KEY (ApplicationId) 
            REFERENCES dbo.Applications (Id) ON DELETE CASCADE
    );
END
GO

-- 7. Skills
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = N'Skills' AND schema_id = SCHEMA_ID(N'dbo'))
BEGIN
    CREATE TABLE dbo.Skills (
        Id UNIQUEIDENTIFIER NOT NULL CONSTRAINT DF_Skills_Id DEFAULT NEWSEQUENTIALID(),
        SkillName NVARCHAR(100) NOT NULL,
        DomainClassification TINYINT NOT NULL, -- 0:Backend, 1:Frontend, 2:DevOps, 3:SoftSkills
        CONSTRAINT PK_Skills PRIMARY KEY CLUSTERED (Id),
        CONSTRAINT UQ_Skills_SkillName UNIQUE (SkillName)
    );
END
GO

-- 8. StudentSkills
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = N'StudentSkills' AND schema_id = SCHEMA_ID(N'dbo'))
BEGIN
    CREATE TABLE dbo.StudentSkills (
        StudentId UNIQUEIDENTIFIER NOT NULL,
        SkillId UNIQUEIDENTIFIER NOT NULL,
        ProficiencyLevel INT NOT NULL,
        CONSTRAINT PK_StudentSkills PRIMARY KEY CLUSTERED (StudentId, SkillId),
        CONSTRAINT CK_StudentSkills_ProficiencyLevel CHECK (ProficiencyLevel BETWEEN 1 AND 5),
        CONSTRAINT FK_StudentSkills_Students_StudentId FOREIGN KEY (StudentId) 
            REFERENCES dbo.Students (Id) ON DELETE CASCADE,
        CONSTRAINT FK_StudentSkills_Skills_SkillId FOREIGN KEY (SkillId) 
            REFERENCES dbo.Skills (Id) ON DELETE CASCADE
    );
END
GO

-- 9. JobSkills
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = N'JobSkills' AND schema_id = SCHEMA_ID(N'dbo'))
BEGIN
    CREATE TABLE dbo.JobSkills (
        JobId UNIQUEIDENTIFIER NOT NULL,
        SkillId UNIQUEIDENTIFIER NOT NULL,
        RequiredImportanceWeight INT NOT NULL,
        CONSTRAINT PK_JobSkills PRIMARY KEY CLUSTERED (JobId, SkillId),
        CONSTRAINT CK_JobSkills_RequiredImportanceWeight CHECK (RequiredImportanceWeight BETWEEN 1 AND 5),
        CONSTRAINT FK_JobSkills_Jobs_JobId FOREIGN KEY (JobId) 
            REFERENCES dbo.Jobs (Id) ON DELETE CASCADE,
        CONSTRAINT FK_JobSkills_Skills_SkillId FOREIGN KEY (SkillId) 
            REFERENCES dbo.Skills (Id) ON DELETE CASCADE
    );
END
GO

-- 10. Notifications
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = N'Notifications' AND schema_id = SCHEMA_ID(N'dbo'))
BEGIN
    CREATE TABLE dbo.Notifications (
        Id UNIQUEIDENTIFIER NOT NULL CONSTRAINT DF_Notifications_Id DEFAULT NEWSEQUENTIALID(),
        TargetUserId UNIQUEIDENTIFIER NOT NULL,
        TextPayload NVARCHAR(500) NOT NULL,
        EventRoutingUrl NVARCHAR(500) NOT NULL,
        IsRead BIT NOT NULL CONSTRAINT DF_Notifications_IsRead DEFAULT 0,
        TimeTriggered DATETIMEOFFSET NOT NULL CONSTRAINT DF_Notifications_TimeTriggered DEFAULT SYSDATETIMEOFFSET(),
        CONSTRAINT PK_Notifications PRIMARY KEY CLUSTERED (Id),
        CONSTRAINT FK_Notifications_AspNetUsers_TargetUserId FOREIGN KEY (TargetUserId) 
            REFERENCES dbo.AspNetUsers (Id) ON DELETE CASCADE
    );
END
GO

-- 11. Assessments
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = N'Assessments' AND schema_id = SCHEMA_ID(N'dbo'))
BEGIN
    CREATE TABLE dbo.Assessments (
        Id UNIQUEIDENTIFIER NOT NULL CONSTRAINT DF_Assessments_Id DEFAULT NEWSEQUENTIALID(),
        StudentId UNIQUEIDENTIFIER NOT NULL,
        SkillId UNIQUEIDENTIFIER NOT NULL,
        AchievedScore INT NOT NULL,
        EarnedDate DATETIMEOFFSET NOT NULL CONSTRAINT DF_Assessments_EarnedDate DEFAULT SYSDATETIMEOFFSET(),
        CONSTRAINT PK_Assessments PRIMARY KEY CLUSTERED (Id),
        CONSTRAINT CK_Assessments_AchievedScore CHECK (AchievedScore BETWEEN 0 AND 100),
        CONSTRAINT FK_Assessments_Students_StudentId FOREIGN KEY (StudentId) 
            REFERENCES dbo.Students (Id) ON DELETE CASCADE,
        CONSTRAINT FK_Assessments_Skills_SkillId FOREIGN KEY (SkillId) 
            REFERENCES dbo.Skills (Id) ON DELETE CASCADE
    );
END
GO

-- 12. CounselorFeedback
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = N'CounselorFeedback' AND schema_id = SCHEMA_ID(N'dbo'))
BEGIN
    CREATE TABLE dbo.CounselorFeedback (
        Id UNIQUEIDENTIFIER NOT NULL CONSTRAINT DF_CounselorFeedback_Id DEFAULT NEWSEQUENTIALID(),
        StudentId UNIQUEIDENTIFIER NOT NULL,
        CounselorUserId UNIQUEIDENTIFIER NOT NULL,
        NarrativeMarkdown NVARCHAR(MAX) NOT NULL,
        MeetingDate DATETIMEOFFSET NOT NULL CONSTRAINT DF_CounselorFeedback_MeetingDate DEFAULT SYSDATETIMEOFFSET(),
        CONSTRAINT PK_CounselorFeedback PRIMARY KEY CLUSTERED (Id),
        CONSTRAINT FK_CounselorFeedback_Students_StudentId FOREIGN KEY (StudentId) 
            REFERENCES dbo.Students (Id) ON DELETE CASCADE,
        CONSTRAINT FK_CounselorFeedback_AspNetUsers_CounselorUserId FOREIGN KEY (CounselorUserId) 
            REFERENCES dbo.AspNetUsers (Id) ON DELETE NO ACTION
    );
END
GO

-- 13. AIHistory
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = N'AIHistory' AND schema_id = SCHEMA_ID(N'dbo'))
BEGIN
    CREATE TABLE dbo.AIHistory (
        Id UNIQUEIDENTIFIER NOT NULL CONSTRAINT DF_AIHistory_Id DEFAULT NEWSEQUENTIALID(),
        UserId UNIQUEIDENTIFIER NOT NULL,
        IntegrationFeature TINYINT NOT NULL, -- 0:AtsScoring, 1:ResumeSuggestions, 2:JobRecommendations, 3:CoverLetter, 4:QuestionBank, 5:MockInterview, 6:SkillGap
        PromptContext NVARCHAR(1000) NOT NULL,
        TokenCost DECIMAL(10,4) NOT NULL,
        PromptTokens INT NOT NULL,
        CompletionTokens INT NOT NULL,
        CreatedAt DATETIMEOFFSET NOT NULL CONSTRAINT DF_AIHistory_CreatedAt DEFAULT SYSDATETIMEOFFSET(),
        CONSTRAINT PK_AIHistory PRIMARY KEY CLUSTERED (Id),
        CONSTRAINT FK_AIHistory_AspNetUsers_UserId FOREIGN KEY (UserId) 
            REFERENCES dbo.AspNetUsers (Id) ON DELETE CASCADE
    );
END
GO

-- 14. OtpCodes
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = N'OtpCodes' AND schema_id = SCHEMA_ID(N'dbo'))
BEGIN
    CREATE TABLE dbo.OtpCodes (
        Id UNIQUEIDENTIFIER NOT NULL CONSTRAINT DF_OtpCodes_Id DEFAULT NEWSEQUENTIALID(),
        UserId UNIQUEIDENTIFIER NOT NULL,
        CodeHash NVARCHAR(128) NOT NULL,
        ExpiresAt DATETIMEOFFSET NOT NULL,
        ConsumedAt DATETIMEOFFSET NULL,
        CreatedAt DATETIMEOFFSET NOT NULL CONSTRAINT DF_OtpCodes_CreatedAt DEFAULT SYSDATETIMEOFFSET(),
        LastSentAt DATETIMEOFFSET NOT NULL CONSTRAINT DF_OtpCodes_LastSentAt DEFAULT SYSDATETIMEOFFSET(),
        CONSTRAINT PK_OtpCodes PRIMARY KEY CLUSTERED (Id),
        CONSTRAINT FK_OtpCodes_AspNetUsers_UserId FOREIGN KEY (UserId) 
            REFERENCES dbo.AspNetUsers (Id) ON DELETE CASCADE
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM dbo.SchemaVersions WHERE ScriptName = N'002_domain_tables.sql')
BEGIN
    INSERT INTO dbo.SchemaVersions (ScriptName) VALUES (N'002_domain_tables.sql');
END
GO

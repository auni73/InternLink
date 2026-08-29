-- 001_identity_tables.sql
-- Description: ASP.NET Core Identity tables with uniqueidentifier (GUID) keys and custom columns

USE [InternLink];
GO

IF EXISTS (SELECT 1 FROM dbo.SchemaVersions WHERE ScriptName = N'001_identity_tables.sql')
BEGIN
    PRINT '001_identity_tables.sql already applied. Skipping.';
    RETURN;
END
GO

-- 1. AspNetRoles
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = N'AspNetRoles' AND schema_id = SCHEMA_ID(N'dbo'))
BEGIN
    CREATE TABLE dbo.AspNetRoles (
        Id UNIQUEIDENTIFIER NOT NULL CONSTRAINT DF_AspNetRoles_Id DEFAULT NEWSEQUENTIALID(),
        Name NVARCHAR(256) NULL,
        NormalizedName NVARCHAR(256) NULL,
        ConcurrencyStamp NVARCHAR(MAX) NULL,
        CONSTRAINT PK_AspNetRoles PRIMARY KEY CLUSTERED (Id)
    );

    CREATE UNIQUE NONCLUSTERED INDEX RoleNameIndex 
    ON dbo.AspNetRoles (NormalizedName) 
    WHERE NormalizedName IS NOT NULL;
END
GO

-- 2. AspNetUsers
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = N'AspNetUsers' AND schema_id = SCHEMA_ID(N'dbo'))
BEGIN
    CREATE TABLE dbo.AspNetUsers (
        Id UNIQUEIDENTIFIER NOT NULL CONSTRAINT DF_AspNetUsers_Id DEFAULT NEWSEQUENTIALID(),
        UserName NVARCHAR(256) NULL,
        NormalizedUserName NVARCHAR(256) NULL,
        Email NVARCHAR(256) NULL,
        NormalizedEmail NVARCHAR(256) NULL,
        EmailConfirmed BIT NOT NULL CONSTRAINT DF_AspNetUsers_EmailConfirmed DEFAULT 0,
        PasswordHash NVARCHAR(MAX) NULL,
        SecurityStamp NVARCHAR(MAX) NULL,
        ConcurrencyStamp NVARCHAR(MAX) NULL,
        PhoneNumber NVARCHAR(MAX) NULL,
        PhoneNumberConfirmed BIT NOT NULL CONSTRAINT DF_AspNetUsers_PhoneNumberConfirmed DEFAULT 0,
        TwoFactorEnabled BIT NOT NULL CONSTRAINT DF_AspNetUsers_TwoFactorEnabled DEFAULT 0,
        LockoutEnd DATETIMEOFFSET NULL,
        LockoutEnabled BIT NOT NULL CONSTRAINT DF_AspNetUsers_LockoutEnabled DEFAULT 1,
        AccessFailedCount INT NOT NULL CONSTRAINT DF_AspNetUsers_AccessFailedCount DEFAULT 0,
        -- Custom columns
        CreatedAt DATETIMEOFFSET NOT NULL CONSTRAINT DF_AspNetUsers_CreatedAt DEFAULT SYSDATETIMEOFFSET(),
        IsActive BIT NOT NULL CONSTRAINT DF_AspNetUsers_IsActive DEFAULT 1,
        CONSTRAINT PK_AspNetUsers PRIMARY KEY CLUSTERED (Id)
    );

    CREATE UNIQUE NONCLUSTERED INDEX UserNameIndex 
    ON dbo.AspNetUsers (NormalizedUserName) 
    WHERE NormalizedUserName IS NOT NULL;

    CREATE NONCLUSTERED INDEX EmailIndex 
    ON dbo.AspNetUsers (NormalizedEmail);
END
GO

-- 3. AspNetRoleClaims
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = N'AspNetRoleClaims' AND schema_id = SCHEMA_ID(N'dbo'))
BEGIN
    CREATE TABLE dbo.AspNetRoleClaims (
        Id INT IDENTITY(1,1) NOT NULL,
        RoleId UNIQUEIDENTIFIER NOT NULL,
        ClaimType NVARCHAR(MAX) NULL,
        ClaimValue NVARCHAR(MAX) NULL,
        CONSTRAINT PK_AspNetRoleClaims PRIMARY KEY CLUSTERED (Id),
        CONSTRAINT FK_AspNetRoleClaims_AspNetRoles_RoleId FOREIGN KEY (RoleId) 
            REFERENCES dbo.AspNetRoles (Id) ON DELETE CASCADE
    );

    CREATE NONCLUSTERED INDEX IX_AspNetRoleClaims_RoleId ON dbo.AspNetRoleClaims (RoleId);
END
GO

-- 4. AspNetUserClaims
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = N'AspNetUserClaims' AND schema_id = SCHEMA_ID(N'dbo'))
BEGIN
    CREATE TABLE dbo.AspNetUserClaims (
        Id INT IDENTITY(1,1) NOT NULL,
        UserId UNIQUEIDENTIFIER NOT NULL,
        ClaimType NVARCHAR(MAX) NULL,
        ClaimValue NVARCHAR(MAX) NULL,
        CONSTRAINT PK_AspNetUserClaims PRIMARY KEY CLUSTERED (Id),
        CONSTRAINT FK_AspNetUserClaims_AspNetUsers_UserId FOREIGN KEY (UserId) 
            REFERENCES dbo.AspNetUsers (Id) ON DELETE CASCADE
    );

    CREATE NONCLUSTERED INDEX IX_AspNetUserClaims_UserId ON dbo.AspNetUserClaims (UserId);
END
GO

-- 5. AspNetUserLogins
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = N'AspNetUserLogins' AND schema_id = SCHEMA_ID(N'dbo'))
BEGIN
    CREATE TABLE dbo.AspNetUserLogins (
        LoginProvider NVARCHAR(128) NOT NULL,
        ProviderKey NVARCHAR(128) NOT NULL,
        ProviderDisplayName NVARCHAR(MAX) NULL,
        UserId UNIQUEIDENTIFIER NOT NULL,
        CONSTRAINT PK_AspNetUserLogins PRIMARY KEY CLUSTERED (LoginProvider, ProviderKey),
        CONSTRAINT FK_AspNetUserLogins_AspNetUsers_UserId FOREIGN KEY (UserId) 
            REFERENCES dbo.AspNetUsers (Id) ON DELETE CASCADE
    );

    CREATE NONCLUSTERED INDEX IX_AspNetUserLogins_UserId ON dbo.AspNetUserLogins (UserId);
END
GO

-- 6. AspNetUserRoles
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = N'AspNetUserRoles' AND schema_id = SCHEMA_ID(N'dbo'))
BEGIN
    CREATE TABLE dbo.AspNetUserRoles (
        UserId UNIQUEIDENTIFIER NOT NULL,
        RoleId UNIQUEIDENTIFIER NOT NULL,
        CONSTRAINT PK_AspNetUserRoles PRIMARY KEY CLUSTERED (UserId, RoleId),
        CONSTRAINT FK_AspNetUserRoles_AspNetUsers_UserId FOREIGN KEY (UserId) 
            REFERENCES dbo.AspNetUsers (Id) ON DELETE CASCADE,
        CONSTRAINT FK_AspNetUserRoles_AspNetRoles_RoleId FOREIGN KEY (RoleId) 
            REFERENCES dbo.AspNetRoles (Id) ON DELETE CASCADE
    );

    CREATE NONCLUSTERED INDEX IX_AspNetUserRoles_RoleId ON dbo.AspNetUserRoles (RoleId);
END
GO

-- 7. AspNetUserTokens
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = N'AspNetUserTokens' AND schema_id = SCHEMA_ID(N'dbo'))
BEGIN
    CREATE TABLE dbo.AspNetUserTokens (
        UserId UNIQUEIDENTIFIER NOT NULL,
        LoginProvider NVARCHAR(128) NOT NULL,
        Name NVARCHAR(128) NOT NULL,
        Value NVARCHAR(MAX) NULL,
        CONSTRAINT PK_AspNetUserTokens PRIMARY KEY CLUSTERED (UserId, LoginProvider, Name),
        CONSTRAINT FK_AspNetUserTokens_AspNetUsers_UserId FOREIGN KEY (UserId) 
            REFERENCES dbo.AspNetUsers (Id) ON DELETE CASCADE
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM dbo.SchemaVersions WHERE ScriptName = N'001_identity_tables.sql')
BEGIN
    INSERT INTO dbo.SchemaVersions (ScriptName) VALUES (N'001_identity_tables.sql');
END
GO

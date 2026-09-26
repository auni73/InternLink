-- =========================================================================
-- 010_monetization_subscriptions.sql
-- InternLink Company Monetization, bKash MFS & Subscription Ledger
-- Schema Truth: No EF migrations. Hand-authored parameterized T-SQL.
-- =========================================================================

USE [InternLink];
GO

IF EXISTS (SELECT 1 FROM dbo.SchemaVersions WHERE ScriptName = N'010_monetization_subscriptions.sql')
BEGIN
    PRINT '010_monetization_subscriptions.sql already applied. Skipping.';
    RETURN;
END
GO

-- 1. Create dbo.SubscriptionPlans table
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = N'SubscriptionPlans' AND schema_id = SCHEMA_ID(N'dbo'))
BEGIN
    CREATE TABLE dbo.SubscriptionPlans (
        Id INT IDENTITY(1,1) PRIMARY KEY,
        Code NVARCHAR(50) NOT NULL UNIQUE,          -- 'FREE_TRIAL', 'STARTER', 'GROWTH', 'ENTERPRISE'
        Name NVARCHAR(100) NOT NULL,                -- 'Starter Tier (30 Days)'
        Description NVARCHAR(300) NULL,
        Price DECIMAL(18,2) NOT NULL DEFAULT 0.00,  -- 1500.00 BDT
        BillingCycleDays INT NOT NULL DEFAULT 30,   -- 30, 90, 365
        MaxActiveJobs INT NOT NULL DEFAULT 3,       -- -1 or >=999 for unlimited
        FeaturesJson NVARCHAR(MAX) NOT NULL DEFAULT '[]',
        IsActive BIT NOT NULL DEFAULT 1,
        DisplayOrder INT NOT NULL DEFAULT 0,
        CreatedAt DATETIMEOFFSET NOT NULL DEFAULT SYSDATETIMEOFFSET()
    );
    PRINT 'Created dbo.SubscriptionPlans table.';
END
GO

-- 2. Create dbo.CompanySubscriptions table
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = N'CompanySubscriptions' AND schema_id = SCHEMA_ID(N'dbo'))
BEGIN
    CREATE TABLE dbo.CompanySubscriptions (
        Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
        CompanyId UNIQUEIDENTIFIER NOT NULL,
        PlanId INT NOT NULL,
        Status TINYINT NOT NULL DEFAULT 1,          -- 0: PendingPayment, 1: Active, 2: Expired, 3: Suspended, 4: Cancelled
        StartDate DATETIMEOFFSET NOT NULL,
        EndDate DATETIMEOFFSET NOT NULL,
        MaxActiveJobsSnapshot INT NOT NULL DEFAULT 3,
        ApprovedByAdminId UNIQUEIDENTIFIER NULL,
        AdminNotes NVARCHAR(500) NULL,
        CreatedAt DATETIMEOFFSET NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        UpdatedAt DATETIMEOFFSET NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        CONSTRAINT FK_CompanySubscriptions_Company FOREIGN KEY (CompanyId) REFERENCES dbo.Companies(Id) ON DELETE CASCADE,
        CONSTRAINT FK_CompanySubscriptions_Plan FOREIGN KEY (PlanId) REFERENCES dbo.SubscriptionPlans(Id),
        CONSTRAINT FK_CompanySubscriptions_Admin FOREIGN KEY (ApprovedByAdminId) REFERENCES dbo.AspNetUsers(Id)
    );

    CREATE INDEX IX_CompanySubscriptions_Company_Status ON dbo.CompanySubscriptions(CompanyId, Status, EndDate);
    PRINT 'Created dbo.CompanySubscriptions table.';
END
GO

-- 3. Create dbo.PaymentTransactions table
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = N'PaymentTransactions' AND schema_id = SCHEMA_ID(N'dbo'))
BEGIN
    CREATE TABLE dbo.PaymentTransactions (
        Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
        CompanyId UNIQUEIDENTIFIER NOT NULL,
        SubscriptionId UNIQUEIDENTIFIER NULL,
        PlanId INT NOT NULL,
        InvoiceNumber NVARCHAR(50) NOT NULL UNIQUE, -- 'INV-202609-XXXXX'
        PaymentMethod NVARCHAR(30) NOT NULL,        -- 'bKash', 'BankTransfer', 'AdminGrant'
        Amount DECIMAL(18,2) NOT NULL,
        Currency NVARCHAR(10) NOT NULL DEFAULT 'BDT',
        Status TINYINT NOT NULL DEFAULT 0,          -- 0: Initiated, 1: Completed, 2: Failed, 3: Cancelled, 4: Refunded
        BkashPaymentId NVARCHAR(100) NULL,
        BkashTrxId NVARCHAR(100) NULL,
        PayerMsisdn NVARCHAR(30) NULL,
        CustomerReference NVARCHAR(100) NULL,      -- Bank reference, sender number, or email proof note
        PaymentExecuteTime DATETIMEOFFSET NULL,
        RawGatewayResponse NVARCHAR(MAX) NULL,
        CreatedAt DATETIMEOFFSET NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        CONSTRAINT FK_PaymentTransactions_Company FOREIGN KEY (CompanyId) REFERENCES dbo.Companies(Id) ON DELETE CASCADE,
        CONSTRAINT FK_PaymentTransactions_Plan FOREIGN KEY (PlanId) REFERENCES dbo.SubscriptionPlans(Id),
        CONSTRAINT FK_PaymentTransactions_Subscription FOREIGN KEY (SubscriptionId) REFERENCES dbo.CompanySubscriptions(Id)
    );

    CREATE INDEX IX_PaymentTransactions_Company ON dbo.PaymentTransactions(CompanyId, CreatedAt DESC);
    CREATE INDEX IX_PaymentTransactions_Invoice ON dbo.PaymentTransactions(InvoiceNumber);
    PRINT 'Created dbo.PaymentTransactions table.';
END
GO

-- 4. Seed Standard Subscription Plans
IF NOT EXISTS (SELECT 1 FROM dbo.SubscriptionPlans WHERE Code = N'FREE_TRIAL')
BEGIN
    INSERT INTO dbo.SubscriptionPlans (Code, Name, Description, Price, BillingCycleDays, MaxActiveJobs, FeaturesJson, IsActive, DisplayOrder)
    VALUES (
        N'FREE_TRIAL',
        N'University Welcome Trial',
        N'Explore the employer portal, post your first engineering internship, and review candidate applications.',
        0.00,
        14,
        1,
        N'["1 Active Internship Post","Full ATS Kanban Pipeline","14 Days Evaluation Period","Student Resume Downloads"]',
        1,
        1
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM dbo.SubscriptionPlans WHERE Code = N'STARTER')
BEGIN
    INSERT INTO dbo.SubscriptionPlans (Code, Name, Description, Price, BillingCycleDays, MaxActiveJobs, FeaturesJson, IsActive, DisplayOrder)
    VALUES (
        N'STARTER',
        N'Starter Monthly',
        N'Ideal for growing tech companies, startups, and consulting firms looking for entry-level engineering talent.',
        1500.00,
        30,
        3,
        N'["Up to 3 Active Vacancies","30 Days Full Platform Access","Full ATS Pipeline & Candidate Status","Direct bKash Automated Invoice","Verified Student Talent Search"]',
        1,
        2
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM dbo.SubscriptionPlans WHERE Code = N'GROWTH')
BEGIN
    INSERT INTO dbo.SubscriptionPlans (Code, Name, Description, Price, BillingCycleDays, MaxActiveJobs, FeaturesJson, IsActive, DisplayOrder)
    VALUES (
        N'GROWTH',
        N'Growth Quarterly (Most Popular)',
        N'Comprehensive quarterly talent acquisition package for established engineering & IT enterprises.',
        3800.00,
        90,
        10,
        N'["Up to 10 Active Vacancies","90 Days Extended Coverage","Priority Placement in Student Feeds","Cumulative Expiry Rollover","Multi-discipline Department Targeting","Dedicated Email & WhatsApp Support"]',
        1,
        3
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM dbo.SubscriptionPlans WHERE Code = N'ENTERPRISE')
BEGIN
    INSERT INTO dbo.SubscriptionPlans (Code, Name, Description, Price, BillingCycleDays, MaxActiveJobs, FeaturesJson, IsActive, DisplayOrder)
    VALUES (
        N'ENTERPRISE',
        N'Annual Corporate Unlimited',
        N'Unlimited year-round recruitment partnership for corporations, multinationals, and top EPC groups.',
        12000.00,
        365,
        999,
        N'["Unlimited Concurrent Vacancies","365 Days Unrestricted Access","Campus Placement Drive Priority","Featured Employer Header Badge","ATS Export & Bulk Candidate Filtering","Dedicated Account Manager & Invoicing"]',
        1,
        4
    );
END
GO

-- 5. Record Migration in dbo.SchemaVersions
INSERT INTO dbo.SchemaVersions (ScriptName, AppliedAt)
VALUES (N'010_monetization_subscriptions.sql', SYSDATETIMEOFFSET());
PRINT '010_monetization_subscriptions.sql migration applied successfully.';
GO

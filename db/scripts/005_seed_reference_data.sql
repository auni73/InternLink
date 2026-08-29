-- 005_seed_reference_data.sql
-- Description: Seed reference skills across Backend, Frontend, DevOps, and SoftSkills

USE [InternLink];
GO

IF EXISTS (SELECT 1 FROM dbo.SchemaVersions WHERE ScriptName = N'005_seed_reference_data.sql')
BEGIN
    PRINT '005_seed_reference_data.sql already applied. Skipping.';
    RETURN;
END
GO

-- 1. Skills Reference Data
-- DomainClassification: 0=Backend, 1=Frontend, 2=DevOps, 3=SoftSkills
MERGE INTO dbo.Skills AS target
USING (VALUES
    -- Backend (0)
    (N'C#', 0),
    (N'ASP.NET Core', 0),
    (N'SQL Server', 0),
    (N'Entity Framework Core', 0),
    (N'Docker', 0),
    (N'REST APIs', 0),
    -- Frontend (1)
    (N'JavaScript', 1),
    (N'Bootstrap 5', 1),
    (N'React', 1),
    (N'HTML5/CSS3', 1),
    (N'TypeScript', 1),
    -- DevOps (2)
    (N'CI/CD Pipelines', 2),
    (N'AWS', 2),
    (N'Kubernetes', 2),
    (N'Git & Version Control', 2),
    -- SoftSkills (3)
    (N'Technical Communication', 3),
    (N'Teamwork & Collaboration', 3),
    (N'Problem Solving', 3),
    (N'Agile/Scrum', 3)
) AS source (SkillName, DomainClassification)
ON target.SkillName = source.SkillName
WHEN NOT MATCHED THEN
    INSERT (SkillName, DomainClassification)
    VALUES (source.SkillName, source.DomainClassification);
GO

IF NOT EXISTS (SELECT 1 FROM dbo.SchemaVersions WHERE ScriptName = N'005_seed_reference_data.sql')
BEGIN
    INSERT INTO dbo.SchemaVersions (ScriptName) VALUES (N'005_seed_reference_data.sql');
END
GO

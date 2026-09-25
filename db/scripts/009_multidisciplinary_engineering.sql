-- 009_multidisciplinary_engineering.sql
-- Description: Multi-disciplinary engineering expansion (CE, EEE, ME, IPE, TE).
-- Adds TargetDepartment to Jobs, DepartmentCode and CategoryName to Skills, and seeds domain reference skills.

USE [InternLink];
GO

IF EXISTS (SELECT 1 FROM dbo.SchemaVersions WHERE ScriptName = N'009_multidisciplinary_engineering.sql')
BEGIN
    PRINT '009_multidisciplinary_engineering.sql already applied. Skipping.';
    RETURN;
END
GO

-- 1. Add TargetDepartment column to dbo.Jobs
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'dbo.Jobs') AND name = N'TargetDepartment')
BEGIN
    ALTER TABLE dbo.Jobs ADD TargetDepartment NVARCHAR(100) NULL;
    PRINT 'Added TargetDepartment column to dbo.Jobs.';
END
GO

-- 2. Add DepartmentCode and CategoryName to dbo.Skills
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'dbo.Skills') AND name = N'DepartmentCode')
BEGIN
    ALTER TABLE dbo.Skills ADD DepartmentCode NVARCHAR(10) NOT NULL CONSTRAINT DF_Skills_DepartmentCode DEFAULT N'CSE';
    PRINT 'Added DepartmentCode column to dbo.Skills.';
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'dbo.Skills') AND name = N'CategoryName')
BEGIN
    ALTER TABLE dbo.Skills ADD CategoryName NVARCHAR(50) NOT NULL CONSTRAINT DF_Skills_CategoryName DEFAULT N'Software';
    PRINT 'Added CategoryName column to dbo.Skills.';
END
GO

-- 3. Update existing seed skills with precise DepartmentCode and CategoryName
UPDATE dbo.Skills
SET DepartmentCode = N'CSE', CategoryName = N'Backend'
WHERE DomainClassification = 0 AND DepartmentCode = N'CSE';

UPDATE dbo.Skills
SET DepartmentCode = N'CSE', CategoryName = N'Frontend'
WHERE DomainClassification = 1 AND DepartmentCode = N'CSE';

UPDATE dbo.Skills
SET DepartmentCode = N'CSE', CategoryName = N'DevOps'
WHERE DomainClassification = 2 AND DepartmentCode = N'CSE';

UPDATE dbo.Skills
SET DepartmentCode = N'GEN', CategoryName = N'Professional Skills'
WHERE DomainClassification = 3;
GO

-- 4. Seed Reference Skills across CE, EEE, ME, IPE, TE, and General Engineering
-- Using MERGE on SkillName so it is safe to rerun anytime
MERGE INTO dbo.Skills AS target
USING (VALUES
    -- Civil Engineering (CE)
    (N'AutoCAD & Civil 3D', 0, N'CE', N'CAD & Modeling'),
    (N'Structural Mechanics & Analysis', 0, N'CE', N'Structural'),
    (N'ETABS & STAAD.Pro', 0, N'CE', N'Structural'),
    (N'Reinforced Concrete Design', 0, N'CE', N'Structural'),
    (N'Geotechnical & Foundation Engineering', 0, N'CE', N'Geotechnical'),
    (N'Surveying & GIS', 0, N'CE', N'Field & Geospatial'),
    (N'Transportation Engineering', 0, N'CE', N'Infrastructure'),
    (N'Environmental & Water Resources', 0, N'CE', N'Environmental'),

    -- Electrical & Electronic Engineering (EEE)
    (N'Circuit Analysis & Design', 0, N'EEE', N'Electronics'),
    (N'MATLAB & Simulink', 0, N'EEE', N'Simulation & Computing'),
    (N'Power System Analysis', 0, N'EEE', N'Power Systems'),
    (N'Microcontrollers & Embedded C', 0, N'EEE', N'Embedded Systems'),
    (N'Digital Logic & Verilog HDL', 0, N'EEE', N'Electronics'),
    (N'Electrical Machines & Drives', 0, N'EEE', N'Power Systems'),
    (N'Telecommunications & Signal Processing', 0, N'EEE', N'Telecommunications'),
    (N'PLC & Industrial Automation', 0, N'EEE', N'Automation & Control'),

    -- Mechanical Engineering (ME)
    (N'SolidWorks & CAD Design', 0, N'ME', N'CAD & Modeling'),
    (N'Thermodynamics & Heat Transfer', 0, N'ME', N'Thermal & Energy'),
    (N'Fluid Mechanics & Hydraulics', 0, N'ME', N'Thermal & Energy'),
    (N'Finite Element Analysis (ANSYS)', 0, N'ME', N'Simulation & Analysis'),
    (N'Manufacturing Processes & CNC', 0, N'ME', N'Manufacturing'),
    (N'HVAC & Refrigeration Systems', 0, N'ME', N'Thermal & Energy'),
    (N'Mechanics of Materials', 0, N'ME', N'Solid Mechanics'),
    (N'Robotics & Mechatronics', 0, N'ME', N'Automation & Control'),

    -- Industrial & Production Engineering (IPE)
    (N'Supply Chain & Logistics Management', 0, N'IPE', N'Supply Chain'),
    (N'Lean Manufacturing & Six Sigma', 0, N'IPE', N'Quality Engineering'),
    (N'Operations Research & Optimization', 0, N'IPE', N'Operations Analysis'),
    (N'Production Planning & Control (PPC)', 0, N'IPE', N'Production Management'),
    (N'Statistical Quality Control (SQC)', 0, N'IPE', N'Quality Engineering'),
    (N'Ergonomics & Industrial Safety', 0, N'IPE', N'Plant Engineering'),
    (N'Total Quality Management (TQM)', 0, N'IPE', N'Quality Engineering'),

    -- Textile Engineering (TE)
    (N'Yarn Manufacturing Technology', 0, N'TE', N'Yarn Production'),
    (N'Fabric Structure & Design', 0, N'TE', N'Fabric Formation'),
    (N'Weaving & Knitting Technology', 0, N'TE', N'Fabric Formation'),
    (N'Textile Wet Processing (Dyeing & Printing)', 0, N'TE', N'Chemical Processing'),
    (N'Apparel Merchandising & Costing', 0, N'TE', N'Apparel & Management'),
    (N'Textile Quality Control & Testing', 0, N'TE', N'Quality & Testing'),
    (N'Garments Manufacturing Technology', 0, N'TE', N'Apparel & Management'),

    -- General / Cross-Cutting Engineering (GEN)
    (N'Engineering Project Management', 3, N'GEN', N'Core Competencies'),
    (N'Engineering Ethics & Workplace Safety', 3, N'GEN', N'Core Competencies'),
    (N'Python for Engineering & Data Analysis', 0, N'GEN', N'Computational Tools'),
    (N'Technical Documentation & Reporting', 3, N'GEN', N'Core Competencies')
) AS source (SkillName, DomainClassification, DepartmentCode, CategoryName)
ON target.SkillName = source.SkillName
WHEN MATCHED THEN
    UPDATE SET 
        DepartmentCode = source.DepartmentCode,
        CategoryName = source.CategoryName
WHEN NOT MATCHED THEN
    INSERT (SkillName, DomainClassification, DepartmentCode, CategoryName)
    VALUES (source.SkillName, source.DomainClassification, source.DepartmentCode, source.CategoryName);
GO

-- 5. Record execution in SchemaVersions ledger
IF NOT EXISTS (SELECT 1 FROM dbo.SchemaVersions WHERE ScriptName = N'009_multidisciplinary_engineering.sql')
BEGIN
    INSERT INTO dbo.SchemaVersions (ScriptName) VALUES (N'009_multidisciplinary_engineering.sql');
    PRINT 'Recorded 009_multidisciplinary_engineering.sql in SchemaVersions.';
END
GO

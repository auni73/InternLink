using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using InternLink.Web.Models;
using InternLink.Web.Models.Enums;

namespace InternLink.Web.Data;

public static class DbSeeder
{
    // The canonical set of roles this application uses.
    // Keep this in sync with: Program.cs policies, [Authorize(Roles=...)], AddToRoleAsync calls.
    private static readonly string[] RequiredRoles = ["Admin", "Counselor", "Company", "Student"];

    // Canonical demo accounts. This is the single source of truth.
    // The Quick Test Login buttons in Login.cshtml reference these same emails/passwords.
    private static readonly (string Email, string Password, string Role)[] DemoAccounts =
    [
        ("admin@internlink.test",      "Admin123!",     "Admin"),
        ("counselor@internlink.test",   "Counselor123!", "Counselor"),
        ("techcorp@internlink.test",    "Company123!",   "Company"),
        ("cloudscale@internlink.test",  "Company123!",   "Company"),
        ("datawave@internlink.test",    "Company123!",   "Company"),
        ("student@internlink.test",     "Student123!",   "Student"),
    ];

    /// <summary>
    /// Ensures all application-required Identity roles exist.
    /// Safe to call on every deployment/startup in every environment — idempotent.
    /// Must run after the database schema is ready and before any request can arrive.
    /// </summary>
    public static async Task SeedRequiredRolesAsync(
        RoleManager<AppRole> roleManager,
        ILogger logger)
    {
        foreach (var roleName in RequiredRoles)
        {
            if (!await roleManager.RoleExistsAsync(roleName))
            {
                var result = await roleManager.CreateAsync(new AppRole(roleName));
                if (result.Succeeded)
                {
                    logger.LogInformation("Created required Identity role: {Role}", roleName);
                }
                else
                {
                    var errors = string.Join(", ", result.Errors.Select(e => e.Description));
                    logger.LogError("Failed to create required Identity role '{Role}': {Errors}", roleName, errors);
                    throw new InvalidOperationException(
                        $"Could not create required Identity role '{roleName}': {errors}");
                }
            }
            else
            {
                logger.LogDebug("Identity role already exists, skipping: {Role}", roleName);
            }
        }

        var rolesAfterSeeding = await roleManager.Roles.Select(r => r.Name).ToListAsync();
        logger.LogInformation("Identity roles after seeding: {Roles}", string.Join(", ", rolesAfterSeeding));
    }

    /// <summary>
    /// Seeds/ensures demo accounts and showcase data in every environment (Development &amp; Production).
    /// Fully idempotent — safe to run on every startup.
    /// </summary>
    public static async Task SeedDemoDataAsync(
        ApplicationDbContext db, 
        UserManager<AppUser> userManager, 
        RoleManager<AppRole> roleManager, 
        ILogger logger)
    {
        logger.LogInformation("Demo account seeding started. Environment: {Env}, Accounts to ensure: {Count}",
            Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "(not set)",
            DemoAccounts.Length);

        // Ensure every demo account exists with correct properties and role.
        var seededUsers = new Dictionary<string, AppUser>();
        foreach (var (email, password, role) in DemoAccounts)
        {
            var user = await EnsureDemoAccountAsync(userManager, email, password, role, logger);
            seededUsers[email] = user;
        }

        var company1User = seededUsers["techcorp@internlink.test"];
        var company2User = seededUsers["cloudscale@internlink.test"];
        var company3User = seededUsers["datawave@internlink.test"];
        var studentUser  = seededUsers["student@internlink.test"];

        // 4. Ensure Company Entities
        var company1 = await db.Companies.FirstOrDefaultAsync(c => c.UserId == company1User.Id);
        if (company1 == null)
        {
            company1 = new Company
            {
                Id = Guid.NewGuid(),
                UserId = company1User.Id,
                CompanyName = "TechCorp Innovations Ltd.",
                CorporateWebsite = "https://techcorp.example.com",
                IndustrySector = "Software Development",
                VerificationStatus = VerificationStatus.Verified,
                CreatedAt = DateTimeOffset.UtcNow
            };
            db.Companies.Add(company1);
            await db.SaveChangesAsync();
            logger.LogInformation("Created Company entity for {Email}", company1User.Email);
        }

        var company2 = await db.Companies.FirstOrDefaultAsync(c => c.UserId == company2User.Id);
        if (company2 == null)
        {
            company2 = new Company
            {
                Id = Guid.NewGuid(),
                UserId = company2User.Id,
                CompanyName = "CloudScale Systems",
                CorporateWebsite = "https://cloudscale.example.com",
                IndustrySector = "Cloud & DevOps Infrastructure",
                VerificationStatus = VerificationStatus.Verified,
                CreatedAt = DateTimeOffset.UtcNow
            };
            db.Companies.Add(company2);
            await db.SaveChangesAsync();
            logger.LogInformation("Created Company entity for {Email}", company2User.Email);
        }

        var company3 = await db.Companies.FirstOrDefaultAsync(c => c.UserId == company3User.Id);
        if (company3 == null)
        {
            company3 = new Company
            {
                Id = Guid.NewGuid(),
                UserId = company3User.Id,
                CompanyName = "DataWave Analytics",
                CorporateWebsite = "https://datawave.example.com",
                IndustrySector = "Data Science & AI Solutions",
                VerificationStatus = VerificationStatus.Verified,
                CreatedAt = DateTimeOffset.UtcNow
            };
            db.Companies.Add(company3);
            await db.SaveChangesAsync();
            logger.LogInformation("Created Company entity for {Email}", company3User.Email);
        }

        // Student user was already ensured above via the DemoAccounts loop.

        // 6. Fetch Reference Skills for Job/Student Associations
        var skills = await db.Skills.ToListAsync();
        var csharpSkill = skills.FirstOrDefault(s => s.SkillName == "C#");
        var aspnetSkill = skills.FirstOrDefault(s => s.SkillName == "ASP.NET Core");
        var sqlSkill = skills.FirstOrDefault(s => s.SkillName == "SQL Server");
        var dockerSkill = skills.FirstOrDefault(s => s.SkillName == "Docker");
        var cicdSkill = skills.FirstOrDefault(s => s.SkillName == "CI/CD Pipelines");
        var jsSkill = skills.FirstOrDefault(s => s.SkillName == "JavaScript");
        var bootstrapSkill = skills.FirstOrDefault(s => s.SkillName == "Bootstrap 5");
        var reactSkill = skills.FirstOrDefault(s => s.SkillName == "React");
        var commSkill = skills.FirstOrDefault(s => s.SkillName == "Technical Communication");

        // 7. Ensure Student Entity & Skills
        var student = await db.Students.Include(s => s.StudentSkills).FirstOrDefaultAsync(s => s.UserId == studentUser.Id);
        if (student == null)
        {
            student = new Student
            {
                Id = Guid.NewGuid(),
                UserId = studentUser.Id,
                FirstName = "Tanvir",
                LastName = "Ahmed",
                CGPA = 3.82m,
                InstitutionalId = "21.01.04.100",
                Department = "Computer Science and Engineering",
                Biography = "Motivated CSE undergraduate passionate about backend web development, relational database design, and building scalable cloud-connected software solutions.",
                Interests = "Web Development, Software Engineering, Cloud Computing, AI Applications",
                CreatedAt = DateTimeOffset.UtcNow
            };

            if (csharpSkill != null) student.StudentSkills.Add(new StudentSkill { StudentId = student.Id, SkillId = csharpSkill.Id, ProficiencyLevel = 4 });
            if (aspnetSkill != null) student.StudentSkills.Add(new StudentSkill { StudentId = student.Id, SkillId = aspnetSkill.Id, ProficiencyLevel = 4 });
            if (sqlSkill != null) student.StudentSkills.Add(new StudentSkill { StudentId = student.Id, SkillId = sqlSkill.Id, ProficiencyLevel = 3 });
            if (jsSkill != null) student.StudentSkills.Add(new StudentSkill { StudentId = student.Id, SkillId = jsSkill.Id, ProficiencyLevel = 3 });

            db.Students.Add(student);
            await db.SaveChangesAsync();
            logger.LogInformation("Created Student entity for {Email}", studentUser.Email);
        }

        // 8. Ensure Realistic Showcase Jobs
        if (!await db.Jobs.AnyAsync())
        {
            var job1 = new Job
            {
                Id = Guid.NewGuid(),
                CompanyId = company1.Id,
                Title = "Junior .NET & ASP.NET Core Developer Intern",
                CoreDescription = "We are looking for an ambitious .NET developer intern to contribute to our core university and enterprise web applications. You will collaborate on ASP.NET Core MVC architectures, design relational databases using SQL Server, and craft clean, maintainable C# code.",
                SelectionCriteria = "Strong understanding of object-oriented programming in C#, basic knowledge of ASP.NET Core MVC, relational database design with SQL Server, and familiarity with Git version control.",
                LocationType = LocationType.Hybrid,
                DeadLine = DateTimeOffset.UtcNow.AddDays(30),
                IsApproved = true,
                IsClosed = false,
                CreatedAt = DateTimeOffset.UtcNow
            };
            if (csharpSkill != null) job1.JobSkills.Add(new JobSkill { JobId = job1.Id, SkillId = csharpSkill.Id, RequiredImportanceWeight = 5 });
            if (aspnetSkill != null) job1.JobSkills.Add(new JobSkill { JobId = job1.Id, SkillId = aspnetSkill.Id, RequiredImportanceWeight = 5 });
            if (sqlSkill != null) job1.JobSkills.Add(new JobSkill { JobId = job1.Id, SkillId = sqlSkill.Id, RequiredImportanceWeight = 4 });
            if (bootstrapSkill != null) job1.JobSkills.Add(new JobSkill { JobId = job1.Id, SkillId = bootstrapSkill.Id, RequiredImportanceWeight = 3 });
            db.Jobs.Add(job1);

            var job2 = new Job
            {
                Id = Guid.NewGuid(),
                CompanyId = company2.Id,
                Title = "Cloud Infrastructure & DevOps Intern",
                CoreDescription = "Join CloudScale Systems to automate cloud infrastructure and continuous delivery pipelines. You will build and optimize Docker container images, automate deployment scripts, and manage cloud resources on AWS.",
                SelectionCriteria = "Familiarity with containerization using Docker, understanding of CI/CD concepts, Linux command-line proficiency, and problem-solving mindset.",
                LocationType = LocationType.Remote,
                DeadLine = DateTimeOffset.UtcNow.AddDays(45),
                IsApproved = true,
                IsClosed = false,
                CreatedAt = DateTimeOffset.UtcNow
            };
            if (dockerSkill != null) job2.JobSkills.Add(new JobSkill { JobId = job2.Id, SkillId = dockerSkill.Id, RequiredImportanceWeight = 5 });
            if (cicdSkill != null) job2.JobSkills.Add(new JobSkill { JobId = job2.Id, SkillId = cicdSkill.Id, RequiredImportanceWeight = 4 });
            if (commSkill != null) job2.JobSkills.Add(new JobSkill { JobId = job2.Id, SkillId = commSkill.Id, RequiredImportanceWeight = 4 });
            db.Jobs.Add(job2);

            var job3 = new Job
            {
                Id = Guid.NewGuid(),
                CompanyId = company1.Id,
                Title = "Frontend Web UI/UX Engineer Intern",
                CoreDescription = "TechCorp is hiring a Frontend Web UI Engineer Intern to build responsive, accessible, and high-performance user interfaces. You will work closely with product designers using Bootstrap 5, JavaScript, and modern HTML/CSS.",
                SelectionCriteria = "Solid foundation in JavaScript, HTML5/CSS3, responsive design using Bootstrap 5, and an eye for UI/UX detail.",
                LocationType = LocationType.OnSite,
                DeadLine = DateTimeOffset.UtcNow.AddDays(20),
                IsApproved = true,
                IsClosed = false,
                CreatedAt = DateTimeOffset.UtcNow
            };
            if (jsSkill != null) job3.JobSkills.Add(new JobSkill { JobId = job3.Id, SkillId = jsSkill.Id, RequiredImportanceWeight = 5 });
            if (bootstrapSkill != null) job3.JobSkills.Add(new JobSkill { JobId = job3.Id, SkillId = bootstrapSkill.Id, RequiredImportanceWeight = 5 });
            if (reactSkill != null) job3.JobSkills.Add(new JobSkill { JobId = job3.Id, SkillId = reactSkill.Id, RequiredImportanceWeight = 3 });
            db.Jobs.Add(job3);

            var job4 = new Job
            {
                Id = Guid.NewGuid(),
                CompanyId = company3.Id,
                Title = "Data Analytics & SQL Backend Intern",
                CoreDescription = "DataWave Analytics is seeking an intern to work on data processing workflows, SQL querying, and backend service integration. You will assist in writing complex SQL queries, generating analytical reports, and optimizing database performance.",
                SelectionCriteria = "Proficiency in SQL querying, basic knowledge of C# or Python, understanding of relational data models, and analytical thinking.",
                LocationType = LocationType.Hybrid,
                DeadLine = DateTimeOffset.UtcNow.AddDays(60),
                IsApproved = true,
                IsClosed = false,
                CreatedAt = DateTimeOffset.UtcNow
            };
            if (sqlSkill != null) job4.JobSkills.Add(new JobSkill { JobId = job4.Id, SkillId = sqlSkill.Id, RequiredImportanceWeight = 5 });
            if (csharpSkill != null) job4.JobSkills.Add(new JobSkill { JobId = job4.Id, SkillId = csharpSkill.Id, RequiredImportanceWeight = 4 });
            if (commSkill != null) job4.JobSkills.Add(new JobSkill { JobId = job4.Id, SkillId = commSkill.Id, RequiredImportanceWeight = 4 });
            db.Jobs.Add(job4);

            await db.SaveChangesAsync();
            logger.LogInformation("Seeded realistic showcase jobs.");
        }

        // ── POST-SEEDING VERIFICATION ──────────────────────────────────────────
        // Re-query every demo account from the database and confirm it actually exists
        // with the correct properties. If any verification fails, throw so the app
        // refuses to start — making the failure impossible to miss in Render logs.
        logger.LogInformation("Verifying all demo accounts in the database...");
        int verified = 0;
        foreach (var (email, _, expectedRole) in DemoAccounts)
        {
            var user = await userManager.FindByEmailAsync(email);
            if (user is null)
            {
                logger.LogCritical("VERIFICATION FAILED: Demo account {Email} was NOT found in dbo.AspNetUsers after seeding.", email);
                throw new InvalidOperationException(
                    $"Demo account '{email}' does not exist in the database after seeding. " +
                    $"The UserManager may be connected to a different database than expected.");
            }

            if (!user.EmailConfirmed)
            {
                logger.LogCritical("VERIFICATION FAILED: Demo account {Email} has EmailConfirmed=false.", email);
                throw new InvalidOperationException($"Demo account '{email}' has EmailConfirmed=false.");
            }

            if (!user.IsActive)
            {
                logger.LogCritical("VERIFICATION FAILED: Demo account {Email} has IsActive=false.", email);
                throw new InvalidOperationException($"Demo account '{email}' has IsActive=false.");
            }

            if (string.IsNullOrEmpty(user.PasswordHash))
            {
                logger.LogCritical("VERIFICATION FAILED: Demo account {Email} has NULL PasswordHash.", email);
                throw new InvalidOperationException($"Demo account '{email}' has a null PasswordHash.");
            }

            if (!await userManager.IsInRoleAsync(user, expectedRole))
            {
                logger.LogCritical("VERIFICATION FAILED: Demo account {Email} is NOT in role {Role}.", email, expectedRole);
                throw new InvalidOperationException(
                    $"Demo account '{email}' is not in role '{expectedRole}'.");
            }

            logger.LogInformation("Verified demo account in database: {Email} (Role={Role}, EmailConfirmed={EC}, IsActive={IA})",
                email, expectedRole, user.EmailConfirmed, user.IsActive);
            verified++;
        }

        logger.LogInformation("Demo account seeding completed successfully. Verified {Count} accounts.", verified);
    }

    /// <summary>
    /// Backward-compatible alias for SeedDemoDataAsync.
    /// </summary>
    public static Task SeedDevelopmentDataAsync(
        ApplicationDbContext db,
        UserManager<AppUser> userManager,
        RoleManager<AppRole> roleManager,
        ILogger logger)
    {
        return SeedDemoDataAsync(db, userManager, roleManager, logger);
    }

    private static async Task<AppUser> EnsureDemoAccountAsync(
        UserManager<AppUser> userManager,
        string email,
        string password,
        string roleName,
        ILogger logger)
    {
        var user = await userManager.FindByEmailAsync(email);
        if (user == null)
        {
            user = new AppUser
            {
                UserName = email,
                Email = email,
                EmailConfirmed = true,
                IsActive = true,
                CreatedAt = DateTimeOffset.UtcNow
            };

            logger.LogInformation("Creating demo account: {Email}", email);

            var result = await userManager.CreateAsync(user, password);
            if (!result.Succeeded)
            {
                var errors = string.Join(", ", result.Errors.Select(e => e.Description));
                logger.LogError("Failed to create demo account {Email}: {Errors}", email, errors);
                throw new InvalidOperationException($"Failed to create demo account '{email}': {errors}");
            }

            logger.LogInformation("Created demo account: {Email} (Id={Id})", email, user.Id);

            var roleResult = await userManager.AddToRoleAsync(user, roleName);
            if (!roleResult.Succeeded)
            {
                var errors = string.Join(", ", roleResult.Errors.Select(e => e.Description));
                logger.LogError("Failed to assign role '{Role}' to demo account {Email}: {Errors}", roleName, email, errors);
                throw new InvalidOperationException($"Failed to assign role '{roleName}' to demo account '{email}': {errors}");
            }

            logger.LogInformation("Assigned role {Role} to {Email}", roleName, email);
        }
        else
        {
            bool updated = false;
            if (!user.EmailConfirmed)
            {
                user.EmailConfirmed = true;
                updated = true;
            }
            if (!user.IsActive)
            {
                user.IsActive = true;
                updated = true;
            }

            if (updated)
            {
                var updateResult = await userManager.UpdateAsync(user);
                if (!updateResult.Succeeded)
                {
                    var errors = string.Join(", ", updateResult.Errors.Select(e => e.Description));
                    logger.LogWarning("Failed to update demo account properties for {Email}: {Errors}", email, errors);
                }
            }

            if (!await userManager.IsInRoleAsync(user, roleName))
            {
                var roleResult = await userManager.AddToRoleAsync(user, roleName);
                if (!roleResult.Succeeded)
                {
                    var errors = string.Join(", ", roleResult.Errors.Select(e => e.Description));
                    logger.LogError("Failed to assign role '{Role}' to existing user {Email}: {Errors}", roleName, email, errors);
                    throw new InvalidOperationException($"Failed to assign role '{roleName}' to existing user '{email}': {errors}");
                }
            }

            logger.LogInformation("Demo account already exists: {Email} (Id={Id})", email, user.Id);
        }

        return user;
    }
}

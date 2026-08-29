using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using InternLink.Web.Models;
using InternLink.Web.Models.Enums;

namespace InternLink.Web.Data;

public static class DbSeeder
{
    public static async Task SeedDevelopmentDataAsync(
        ApplicationDbContext db, 
        UserManager<AppUser> userManager, 
        RoleManager<AppRole> roleManager, 
        ILogger logger)
    {
        // 1. Guard: Only seed if database has not been seeded yet
        if (await db.Companies.AnyAsync())
        {
            logger.LogInformation("Database already seeded. Skipping DbSeeder.");
            return;
        }

        logger.LogInformation("Starting development database seeding...");

        // 2. Seed Identity Roles
        string[] roles = ["Admin", "Counselor", "Company", "Student"];
        foreach (var role in roles)
        {
            if (!await roleManager.RoleExistsAsync(role))
            {
                await roleManager.CreateAsync(new AppRole(role));
                logger.LogInformation("Created role: {Role}", role);
            }
        }

        // 3. Seed Admin User
        var adminEmail = "admin@internlink.test";
        var adminUser = await userManager.FindByEmailAsync(adminEmail);
        if (adminUser == null)
        {
            adminUser = new AppUser
            {
                UserName = adminEmail,
                Email = adminEmail,
                EmailConfirmed = true,
                IsActive = true,
                CreatedAt = DateTimeOffset.UtcNow
            };
            var result = await userManager.CreateAsync(adminUser, "Admin123!");
            if (result.Succeeded)
            {
                await userManager.AddToRoleAsync(adminUser, "Admin");
                logger.LogInformation("Seeded Admin user: {Email}", adminEmail);
            }
            else
            {
                logger.LogError("Failed to create admin user: {Errors}", string.Join(", ", result.Errors.Select(e => e.Description)));
            }
        }

        // 4. Seed Counselor User
        var counselorEmail = "counselor@internlink.test";
        var counselorUser = await userManager.FindByEmailAsync(counselorEmail);
        if (counselorUser == null)
        {
            counselorUser = new AppUser
            {
                UserName = counselorEmail,
                Email = counselorEmail,
                EmailConfirmed = true,
                IsActive = true,
                CreatedAt = DateTimeOffset.UtcNow
            };
            var result = await userManager.CreateAsync(counselorUser, "Counselor123!");
            if (result.Succeeded)
            {
                await userManager.AddToRoleAsync(counselorUser, "Counselor");
                logger.LogInformation("Seeded Counselor user: {Email}", counselorEmail);
            }
        }

        // 5. Seed Companies
        var company1User = new AppUser
        {
            UserName = "techcorp@internlink.test",
            Email = "techcorp@internlink.test",
            EmailConfirmed = true,
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow
        };
        await userManager.CreateAsync(company1User, "Company123!");
        await userManager.AddToRoleAsync(company1User, "Company");

        var company1 = new Company
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

        var company2User = new AppUser
        {
            UserName = "cloudscale@internlink.test",
            Email = "cloudscale@internlink.test",
            EmailConfirmed = true,
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow
        };
        await userManager.CreateAsync(company2User, "Company123!");
        await userManager.AddToRoleAsync(company2User, "Company");

        var company2 = new Company
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

        var company3User = new AppUser
        {
            UserName = "datawave@internlink.test",
            Email = "datawave@internlink.test",
            EmailConfirmed = true,
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow
        };
        await userManager.CreateAsync(company3User, "Company123!");
        await userManager.AddToRoleAsync(company3User, "Company");

        var company3 = new Company
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

        // 7. Seed Realistic Jobs (Approved=1, Closed=0, future deadlines)
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

        // 8. Seed Student (with realistic profile and partial overlapping skills)
        var studentUser = new AppUser
        {
            UserName = "student@internlink.test",
            Email = "student@internlink.test",
            EmailConfirmed = true,
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow
        };
        await userManager.CreateAsync(studentUser, "Student123!");
        await userManager.AddToRoleAsync(studentUser, "Student");

        var student = new Student
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

        // Partial overlap: C# (4), ASP.NET Core (4), SQL Server (3), JavaScript (3)
        // Gaps in Docker, DevOps, CI/CD — perfect for skill gap and recommendation testing
        if (csharpSkill != null) student.StudentSkills.Add(new StudentSkill { StudentId = student.Id, SkillId = csharpSkill.Id, ProficiencyLevel = 4 });
        if (aspnetSkill != null) student.StudentSkills.Add(new StudentSkill { StudentId = student.Id, SkillId = aspnetSkill.Id, ProficiencyLevel = 4 });
        if (sqlSkill != null) student.StudentSkills.Add(new StudentSkill { StudentId = student.Id, SkillId = sqlSkill.Id, ProficiencyLevel = 3 });
        if (jsSkill != null) student.StudentSkills.Add(new StudentSkill { StudentId = student.Id, SkillId = jsSkill.Id, ProficiencyLevel = 3 });

        db.Students.Add(student);
        await db.SaveChangesAsync();

        logger.LogInformation("Development database seeding completed successfully.");
    }
}

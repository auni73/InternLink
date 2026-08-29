using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using InternLink.Web.Models;
using InternLink.Web.Models.Enums;

namespace InternLink.Web.Data;

public class ApplicationDbContext : IdentityDbContext<AppUser, AppRole, Guid>
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public DbSet<Student> Students => Set<Student>();
    public DbSet<Company> Companies => Set<Company>();
    public DbSet<Job> Jobs => Set<Job>();
    public DbSet<Application> Applications => Set<Application>();
    public DbSet<Interview> Interviews => Set<Interview>();
    public DbSet<Resume> Resumes => Set<Resume>();
    public DbSet<Skill> Skills => Set<Skill>();
    public DbSet<StudentSkill> StudentSkills => Set<StudentSkill>();
    public DbSet<JobSkill> JobSkills => Set<JobSkill>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<Assessment> Assessments => Set<Assessment>();
    public DbSet<CounselorFeedback> CounselorFeedbacks => Set<CounselorFeedback>();
    public DbSet<AIHistory> AIHistories => Set<AIHistory>();
    public DbSet<OtpCode> OtpCodes => Set<OtpCode>();
    public DbSet<MockInterviewSession> MockInterviewSessions => Set<MockInterviewSession>();
    public DbSet<SchemaVersion> SchemaVersions => Set<SchemaVersion>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        // Note: Delete/cascade behaviors live in the hand-authored SQL scripts (db/scripts/002_domain_tables.sql).
        // EF Core acts as a materializer and query mapper, never a schema owner.

        // 1. Identity Table Names
        builder.Entity<AppUser>(b => b.ToTable("AspNetUsers"));
        builder.Entity<AppRole>(b => b.ToTable("AspNetRoles"));

        // 2. SchemaVersions
        builder.Entity<SchemaVersion>(b =>
        {
            b.ToTable("SchemaVersions");
            b.HasKey(s => s.ScriptName);
        });

        // 3. Students
        builder.Entity<Student>(b =>
        {
            b.ToTable("Students");
            b.HasKey(s => s.Id);
            b.Property(s => s.CGPA).HasPrecision(3, 2);
            b.HasOne(s => s.User)
             .WithOne(u => u.StudentProfile)
             .HasForeignKey<Student>(s => s.UserId);
        });

        // 4. Companies
        builder.Entity<Company>(b =>
        {
            b.ToTable("Companies");
            b.HasKey(c => c.Id);
            b.Property(c => c.VerificationStatus).HasConversion<byte>();
            b.Property(c => c.AdminRejectionReason).HasMaxLength(500);
            b.HasOne(c => c.User)
             .WithOne(u => u.CompanyProfile)
             .HasForeignKey<Company>(c => c.UserId);
        });

        // 5. Jobs
        builder.Entity<Job>(b =>
        {
            b.ToTable("Jobs");
            b.HasKey(j => j.Id);
            b.Property(j => j.LocationType).HasConversion<byte>();
            b.HasOne(j => j.Company)
             .WithMany(c => c.Jobs)
             .HasForeignKey(j => j.CompanyId);
        });

        // 6. Resumes
        builder.Entity<Resume>(b =>
        {
            b.ToTable("Resumes");
            b.HasKey(r => r.Id);
            b.HasOne(r => r.Student)
             .WithMany(s => s.Resumes)
             .HasForeignKey(r => r.StudentId);
        });

        // 7. Applications
        builder.Entity<Application>(b =>
        {
            b.ToTable("Applications");
            b.HasKey(a => a.Id);
            b.Property(a => a.ApplicationStatus).HasConversion<byte>();
            b.HasOne(a => a.Job)
             .WithMany(j => j.Applications)
             .HasForeignKey(a => a.JobId);
            b.HasOne(a => a.Student)
             .WithMany(s => s.Applications)
             .HasForeignKey(a => a.StudentId);
            b.HasOne(a => a.AttachedResume)
             .WithMany(r => r.Applications)
             .HasForeignKey(a => a.AttachedResumeId);
        });

        // 8. Interviews
        builder.Entity<Interview>(b =>
        {
            b.ToTable("Interviews");
            b.HasKey(i => i.Id);
            b.Property(i => i.StatusIndicator).HasConversion<byte>();
            b.HasOne(i => i.Application)
             .WithMany(a => a.Interviews)
             .HasForeignKey(i => i.ApplicationId);
        });

        // 9. Skills
        builder.Entity<Skill>(b =>
        {
            b.ToTable("Skills");
            b.HasKey(s => s.Id);
            b.Property(s => s.DomainClassification).HasConversion<byte>();
        });

        // 10. StudentSkills (Composite PK)
        builder.Entity<StudentSkill>(b =>
        {
            b.ToTable("StudentSkills");
            b.HasKey(ss => new { ss.StudentId, ss.SkillId });
            b.HasOne(ss => ss.Student)
             .WithMany(s => s.StudentSkills)
             .HasForeignKey(ss => ss.StudentId);
            b.HasOne(ss => ss.Skill)
             .WithMany(s => s.StudentSkills)
             .HasForeignKey(ss => ss.SkillId);
        });

        // 11. JobSkills (Composite PK)
        builder.Entity<JobSkill>(b =>
        {
            b.ToTable("JobSkills");
            b.HasKey(js => new { js.JobId, js.SkillId });
            b.HasOne(js => js.Job)
             .WithMany(j => j.JobSkills)
             .HasForeignKey(js => js.JobId);
            b.HasOne(js => js.Skill)
             .WithMany(s => s.JobSkills)
             .HasForeignKey(js => js.SkillId);
        });

        // 12. Notifications
        builder.Entity<Notification>(b =>
        {
            b.ToTable("Notifications");
            b.HasKey(n => n.Id);
            b.HasOne(n => n.TargetUser)
             .WithMany(u => u.Notifications)
             .HasForeignKey(n => n.TargetUserId);
        });

        // 13. Assessments
        builder.Entity<Assessment>(b =>
        {
            b.ToTable("Assessments");
            b.HasKey(a => a.Id);
            b.HasOne(a => a.Student)
             .WithMany(s => s.Assessments)
             .HasForeignKey(a => a.StudentId);
            b.HasOne(a => a.Skill)
             .WithMany(s => s.Assessments)
             .HasForeignKey(a => a.SkillId);
        });

        // 14. CounselorFeedback
        builder.Entity<CounselorFeedback>(b =>
        {
            b.ToTable("CounselorFeedback");
            b.HasKey(cf => cf.Id);
            b.HasOne(cf => cf.Student)
             .WithMany(s => s.CounselorFeedbacks)
             .HasForeignKey(cf => cf.StudentId);
            b.HasOne(cf => cf.CounselorUser)
             .WithMany(u => u.GivenCounselorFeedbacks)
             .HasForeignKey(cf => cf.CounselorUserId);
        });

        // 15. AIHistory
        builder.Entity<AIHistory>(b =>
        {
            b.ToTable("AIHistory");
            b.HasKey(ai => ai.Id);
            b.Property(ai => ai.IntegrationFeature).HasConversion<byte>();
            b.Property(ai => ai.TokenCost).HasPrecision(10, 4);
            b.HasOne(ai => ai.User)
             .WithMany(u => u.AIHistories)
             .HasForeignKey(ai => ai.UserId);
        });

        // 16. OtpCodes
        builder.Entity<OtpCode>(b =>
        {
            b.ToTable("OtpCodes");
            b.HasKey(o => o.Id);
            b.HasOne(o => o.User)
             .WithMany(u => u.OtpCodes)
             .HasForeignKey(o => o.UserId);
        });

        // 17. MockInterviewSessions
        builder.Entity<MockInterviewSession>(b =>
        {
            b.ToTable("MockInterviewSessions");
            b.HasKey(m => m.Id);
            b.Property(m => m.Role).HasMaxLength(100);
            b.Property(m => m.Status).HasConversion<byte>();
            b.HasOne(m => m.Student)
             .WithMany(s => s.MockInterviewSessions)
             .HasForeignKey(m => m.StudentId);
            b.HasOne(m => m.Job)
             .WithMany()
             .HasForeignKey(m => m.JobId);
        });
    }
}

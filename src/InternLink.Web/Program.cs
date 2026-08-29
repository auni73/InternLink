using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using InternLink.Web.Data;
using InternLink.Web.Models;
using InternLink.Web.Repositories.Interface;
using InternLink.Web.Repositories.Implementation;
using InternLink.Web.Services.AI;
using InternLink.Web.Services.Auth;
using InternLink.Web.Services.Dashboard;
using InternLink.Web.Services.Email;
using InternLink.Web.Services.Recommendation;
using InternLink.Web.Services.Vectors;

var builder = WebApplication.CreateBuilder(args);

// 1. Add services to the container. AutoValidateAntiforgeryToken makes every POST antiforgery-protected by default.
builder.Services.AddControllersWithViews(options =>
{
    options.Filters.Add(new AutoValidateAntiforgeryTokenAttribute());
});

// Antiforgery header aligns with the fetch wrapper (wwwroot/js/api.js) sending X-CSRF-TOKEN.
builder.Services.AddAntiforgery(options => options.HeaderName = "X-CSRF-TOKEN");

// 2. Register ApplicationDbContext on the hand-authored schema
var connectionString = builder.Configuration.GetConnectionString("InternLinkDb")
    ?? throw new InvalidOperationException("Connection string 'InternLinkDb' not found in configuration.");

builder.Services.AddDbContext<ApplicationDbContext>(options =>
{
    options.UseSqlServer(connectionString, sqlOptions =>
    {
        sqlOptions.EnableRetryOnFailure(maxRetryCount: 3);
    });
});

// 3. Configure ASP.NET Core Identity with AppUser & AppRole (Guid keys)
builder.Services.AddIdentity<AppUser, AppRole>(options =>
{
    options.Password.RequireDigit = true;
    options.Password.RequiredLength = 8;
    options.Password.RequireNonAlphanumeric = true;
    options.Password.RequireUppercase = true;
    options.Password.RequireLowercase = true;
    options.User.RequireUniqueEmail = true;
    options.SignIn.RequireConfirmedEmail = true;
    options.Lockout.MaxFailedAccessAttempts = 5;
    options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
})
.AddEntityFrameworkStores<ApplicationDbContext>()
.AddDefaultTokenProviders();

// Revalidate the security stamp every 5 minutes so suspends/role changes kill live sessions promptly.
builder.Services.Configure<SecurityStampValidatorOptions>(options =>
{
    options.ValidationInterval = TimeSpan.FromMinutes(5);
});

// Per-role authorization policies consumed by each Area's base controller.
builder.Services.AddAuthorizationBuilder()
    .AddPolicy("StudentOnly", policy => policy.RequireRole("Student"))
    .AddPolicy("CompanyOnly", policy => policy.RequireRole("Company"))
    .AddPolicy("AdminOnly", policy => policy.RequireRole("Admin"))
    .AddPolicy("CounselorOnly", policy => policy.RequireRole("Counselor"));

// Classic cookie auth for this same-origin MVC app (no JWT).
builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Account/Login";
    options.AccessDeniedPath = "/Account/Denied";
    options.Cookie.HttpOnly = true;
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.Cookie.SecurePolicy = builder.Environment.IsDevelopment()
        ? CookieSecurePolicy.SameAsRequest
        : CookieSecurePolicy.Always;
    options.ExpireTimeSpan = TimeSpan.FromDays(7);
    options.SlidingExpiration = true;
});

// 4. Register Repositories (Scoped)
builder.Services.AddScoped<IJobRepository, JobRepository>();
builder.Services.AddScoped<IApplicationRepository, ApplicationRepository>();
builder.Services.AddScoped<IStudentRepository, StudentRepository>();
builder.Services.AddScoped<ICompanyRepository, CompanyRepository>();
builder.Services.AddScoped<IOtpRepository, OtpRepository>();
builder.Services.AddScoped<IDashboardRepository, DashboardRepository>();
builder.Services.AddScoped<IResumeRepository, ResumeRepository>();
builder.Services.AddScoped<ISkillRepository, SkillRepository>();
builder.Services.AddScoped<IAssessmentRepository, AssessmentRepository>();
builder.Services.AddScoped<IAnalyticsRepository, AnalyticsRepository>();
builder.Services.AddScoped<ICounselorRepository, CounselorRepository>();
builder.Services.AddScoped<IAdminModerationRepository, AdminModerationRepository>();
builder.Services.AddScoped<IAIHistoryRepository, AIHistoryRepository>();

// AI gateway: rotating key pool is a singleton so cooldowns are shared across every request.
builder.Services.Configure<GeminiOptions>(builder.Configuration.GetSection(GeminiOptions.SectionName));
builder.Services.AddSingleton<IGeminiKeyPool, GeminiKeyPool>();
builder.Services.AddHttpClient<IGeminiClient, GeminiClient>((sp, client) =>
{
    var gemini = sp.GetRequiredService<IOptions<GeminiOptions>>().Value;
    client.BaseAddress = new Uri(gemini.BaseAddress);
    client.Timeout = TimeSpan.FromSeconds(Math.Max(1, gemini.TimeoutSeconds));
});
builder.Services.AddHttpClient<IEmbeddingClient, GeminiEmbeddingClient>((sp, client) =>
{
    var gemini = sp.GetRequiredService<IOptions<GeminiOptions>>().Value;
    client.BaseAddress = new Uri(gemini.BaseAddress);
    client.Timeout = TimeSpan.FromSeconds(Math.Max(1, gemini.TimeoutSeconds));
});

// Job vector index. The store is a singleton because the Qdrant client multiplexes one gRPC channel.
builder.Services.Configure<QdrantOptions>(builder.Configuration.GetSection(QdrantOptions.SectionName));
builder.Services.AddSingleton<QdrantJobVectorStore>();
builder.Services.AddSingleton<IJobVectorStore>(sp => sp.GetRequiredService<QdrantJobVectorStore>());
builder.Services.AddSingleton<IVectorSearch>(sp => sp.GetRequiredService<QdrantJobVectorStore>());
builder.Services.AddSingleton<IJobIndexQueue, JobIndexQueue>();
builder.Services.AddHostedService<JobVectorIndexer>();

// Recommendations cache per student for an hour; the key hashes their skill set so edits bust it.
builder.Services.AddMemoryCache();
builder.Services.AddScoped<IRecommendationService, RecommendationService>();

// Markdown Sanitization & Rendering Service (with DisableHtml)
builder.Services.AddSingleton<InternLink.Web.Services.IMarkdownService, InternLink.Web.Services.MarkdownService>();

// Storage & Resume PDF Services
builder.Services.AddSingleton<InternLink.Web.Services.Storage.IFileStorage, InternLink.Web.Services.Storage.DiskFileStorage>();
builder.Services.AddSingleton<InternLink.Web.Services.Resume.IPdfRenderer, InternLink.Web.Services.Resume.QuestPdfResumeRenderer>();
builder.Services.AddScoped<InternLink.Web.Services.Resume.IResumeService, InternLink.Web.Services.Resume.ResumeService>();
builder.Services.AddScoped<InternLink.Web.Services.Resume.IResumeAnalysisService, InternLink.Web.Services.Resume.ResumeAnalysisService>();

// Full-Text Search capability service
builder.Services.AddSingleton<InternLink.Web.Helpers.IFtsCapabilityService, InternLink.Web.Helpers.FtsCapabilityService>();

// Skill Assessment Question & Session services
builder.Services.AddSingleton<InternLink.Web.Services.Assessment.IAssessmentQuestionProvider, InternLink.Web.Services.Assessment.AssessmentQuestionProvider>();
builder.Services.AddSingleton<InternLink.Web.Services.Assessment.IAssessmentSessionService, InternLink.Web.Services.Assessment.AssessmentSessionService>();
builder.Services.AddScoped<InternLink.Web.Services.Skills.IStudentSkillService, InternLink.Web.Services.Skills.StudentSkillService>();

// Per-area dashboard services (server-rendered stat cards).
builder.Services.AddScoped<IStudentDashboardService, StudentDashboardService>();
builder.Services.AddScoped<ICompanyDashboardService, CompanyDashboardService>();
builder.Services.AddScoped<IAdminDashboardService, AdminDashboardService>();
builder.Services.AddScoped<ICounselorDashboardService, CounselorDashboardService>();

// OTP second factor: repository-backed service with an injectable clock for testability.
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddScoped<IOtpService, OtpService>();
builder.Services.AddSingleton<PendingLoginTokenService>();
builder.Services.AddSingleton<DevOtpStore>();

// Email sender: write OTP codes/links to console in Development, send via MailKit SMTP otherwise.
if (builder.Environment.IsDevelopment())
{
    builder.Services.AddSingleton<IEmailSender, DevEmailSender>();
}
else
{
    builder.Services.AddSingleton<IEmailSender, MailKitEmailSender>();
}

// Fixed-window rate limiting on the auth POST endpoints (Login/VerifyOtp/ResendOtp): 10/min/IP.
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("auth", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 10,
                Window = TimeSpan.FromMinutes(1)
            }));
});

var app = builder.Build();

// 5. Startup Sanity Probe and Development Seeding (Development only)
if (app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    var userManager = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
    var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<AppRole>>();

    try
    {
        if (await db.Database.CanConnectAsync())
        {
            var appliedScriptsCount = await db.Database
                .SqlQueryRaw<int>("SELECT COUNT(*) AS Value FROM dbo.SchemaVersions")
                .FirstOrDefaultAsync();

            if (appliedScriptsCount == 0)
            {
                logger.LogWarning("SchemaVersions table is empty. Please run db/scripts in order (see db/scripts/README.md).");
            }
            else
            {
                logger.LogInformation("Database connection successful. Applied schema scripts count: {Count}", appliedScriptsCount);
                
                // Seed development data (roles, admin, counselor, companies, jobs, student)
                await DbSeeder.SeedDevelopmentDataAsync(db, userManager, roleManager, logger);
            }
        }
        else
        {
            logger.LogError("Database connection failed. Ensure SQL Server is running and run db/scripts in order — see db/scripts/README.md");
        }
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Database initialization/seeding probe failed. Run db/scripts in order — see db/scripts/README.md");
    }
}

// 6. Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseRateLimiter();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "areas",
    pattern: "{area:exists}/{controller=Home}/{action=Index}/{id?}");

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();

using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using InternLink.Web.Data;
using InternLink.Web.Models;
using InternLink.Web.Repositories.Interface;
using InternLink.Web.Repositories.Implementation;
using InternLink.Web.Services.Email;

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

// Email sender: log confirmation links to console in Development, send via SMTP otherwise.
if (builder.Environment.IsDevelopment())
{
    builder.Services.AddSingleton<IAppEmailSender, DevConsoleEmailSender>();
}
else
{
    builder.Services.AddSingleton<IAppEmailSender, SmtpEmailSender>();
}

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

app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "areas",
    pattern: "{area:exists}/{controller=Home}/{action=Index}/{id?}");

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();

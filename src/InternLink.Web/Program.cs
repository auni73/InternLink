using Microsoft.EntityFrameworkCore;
using InternLink.Web.Data;
using InternLink.Web.Repositories.Interface;
using InternLink.Web.Repositories.Implementation;

var builder = WebApplication.CreateBuilder(args);

// 1. Add services to the container.
builder.Services.AddControllersWithViews();

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

// 3. Register Repositories (Scoped)
builder.Services.AddScoped<IJobRepository, JobRepository>();
builder.Services.AddScoped<IApplicationRepository, ApplicationRepository>();
builder.Services.AddScoped<IStudentRepository, StudentRepository>();
builder.Services.AddScoped<ICompanyRepository, CompanyRepository>();

var app = builder.Build();

// 4. Startup Sanity Probe (Development only)
if (app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

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
            }
        }
        else
        {
            logger.LogError("Database connection failed. Ensure SQL Server is running and run db/scripts in order — see db/scripts/README.md");
        }
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Database sanity probe failed. Run db/scripts in order — see db/scripts/README.md");
    }
}

// 5. Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthorization();

app.MapControllerRoute(
    name: "areas",
    pattern: "{area:exists}/{controller=Home}/{action=Index}/{id?}");

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();

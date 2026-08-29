namespace InternLink.Web.Services.Storage;

public class DiskFileStorage : IFileStorage
{
    private readonly string _resumeRoot;
    private readonly ILogger<DiskFileStorage> _logger;

    public DiskFileStorage(IConfiguration configuration, ILogger<DiskFileStorage> logger)
    {
        _logger = logger;
        var configPath = configuration["Storage:ResumeRoot"];
        if (string.IsNullOrWhiteSpace(configPath))
        {
            configPath = Path.Combine(AppContext.BaseDirectory, "App_Data", "resumes");
        }

        _resumeRoot = configPath;
    }

    public async Task<string> SaveResumePdfAsync(Guid studentId, Guid resumeId, byte[] pdfBytes, CancellationToken ct = default)
    {
        var studentFolder = Path.Combine(_resumeRoot, studentId.ToString("D"));
        Directory.CreateDirectory(studentFolder);

        var filePath = Path.Combine(studentFolder, $"{resumeId:D}.pdf");
        await File.WriteAllBytesAsync(filePath, pdfBytes, ct);
        _logger.LogInformation("Saved resume PDF for student {StudentId} at {FilePath}", studentId, filePath);

        return filePath;
    }

    public Task<Stream?> OpenReadAsync(string relativeOrFullPath, CancellationToken ct = default)
    {
        var fullPath = GetPhysicalPath(relativeOrFullPath);
        if (!File.Exists(fullPath))
        {
            return Task.FromResult<Stream?>(null);
        }

        Stream stream = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read);
        return Task.FromResult<Stream?>(stream);
    }

    public bool Exists(string relativeOrFullPath)
    {
        var fullPath = GetPhysicalPath(relativeOrFullPath);
        return File.Exists(fullPath);
    }

    public string GetPhysicalPath(string relativeOrFullPath)
    {
        if (Path.IsPathRooted(relativeOrFullPath))
        {
            return relativeOrFullPath;
        }

        return Path.Combine(_resumeRoot, relativeOrFullPath);
    }
}

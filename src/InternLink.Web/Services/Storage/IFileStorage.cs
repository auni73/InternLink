namespace InternLink.Web.Services.Storage;

public interface IFileStorage
{
    Task<string> SaveResumePdfAsync(Guid studentId, Guid resumeId, byte[] pdfBytes, CancellationToken ct = default);
    Task<Stream?> OpenReadAsync(string relativeOrFullPath, CancellationToken ct = default);
    bool Exists(string relativeOrFullPath);
    string GetPhysicalPath(string relativeOrFullPath);
}

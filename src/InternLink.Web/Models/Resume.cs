namespace InternLink.Web.Models;

public class Resume
{
    public Guid Id { get; set; }
    public Guid StudentId { get; set; }
    public string? DocumentPath { get; set; }
    public string DynamicJsonData { get; set; } = "{}";
    public DateTimeOffset LastModified { get; set; } = DateTimeOffset.UtcNow;

    // Navigation properties
    public virtual Student Student { get; set; } = null!;
    public virtual ICollection<Application> Applications { get; set; } = new List<Application>();
}

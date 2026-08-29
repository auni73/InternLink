namespace InternLink.Web.Models;

public class SchemaVersion
{
    public string ScriptName { get; set; } = string.Empty;
    public DateTimeOffset AppliedAt { get; set; } = DateTimeOffset.UtcNow;
}

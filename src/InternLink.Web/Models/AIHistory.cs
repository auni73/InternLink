using InternLink.Web.Models.Enums;

namespace InternLink.Web.Models;

public class AIHistory
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public IntegrationFeature IntegrationFeature { get; set; }
    public string PromptContext { get; set; } = string.Empty;
    public decimal TokenCost { get; set; }
    public int PromptTokens { get; set; }
    public int CompletionTokens { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    // Navigation properties
    public virtual AppUser User { get; set; } = null!;
}

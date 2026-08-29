using InternLink.Web.Models;

namespace InternLink.Web.Repositories.Interface;

public interface IAIHistoryRepository
{
    /// <summary>Appends one row to the AI token ledger. PromptContext is truncated to the column width.</summary>
    Task RecordAsync(AIHistory entry, CancellationToken ct = default);
}

using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using InternLink.Web.Data;
using InternLink.Web.Models;
using InternLink.Web.Repositories.Interface;

namespace InternLink.Web.Repositories.Implementation;

public class AIHistoryRepository : IAIHistoryRepository
{
    public const int PromptContextMaxLength = 1000;

    private readonly ApplicationDbContext _db;

    public AIHistoryRepository(ApplicationDbContext db)
    {
        _db = db;
    }

    public async Task RecordAsync(AIHistory entry, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(entry);

        var parameters = new[]
        {
            new SqlParameter("@id", SqlDbType.UniqueIdentifier)
            {
                Value = entry.Id == Guid.Empty ? Guid.NewGuid() : entry.Id
            },
            new SqlParameter("@userId", SqlDbType.UniqueIdentifier) { Value = entry.UserId },
            new SqlParameter("@feature", SqlDbType.TinyInt) { Value = (byte)entry.IntegrationFeature },
            new SqlParameter("@promptContext", SqlDbType.NVarChar, PromptContextMaxLength)
            {
                Value = Truncate(entry.PromptContext, PromptContextMaxLength)
            },
            new SqlParameter("@tokenCost", SqlDbType.Decimal)
            {
                Value = entry.TokenCost,
                Precision = 10,
                Scale = 4
            },
            new SqlParameter("@promptTokens", SqlDbType.Int) { Value = entry.PromptTokens },
            new SqlParameter("@completionTokens", SqlDbType.Int) { Value = entry.CompletionTokens },
            new SqlParameter("@createdAt", SqlDbType.DateTimeOffset) { Value = entry.CreatedAt }
        };

        const string sql = @"
            INSERT INTO dbo.AIHistory 
                (Id, UserId, IntegrationFeature, PromptContext, TokenCost, PromptTokens, CompletionTokens, CreatedAt)
            VALUES 
                (@id, @userId, @feature, @promptContext, @tokenCost, @promptTokens, @completionTokens, @createdAt)";

        await _db.Database.ExecuteSqlRawAsync(sql, parameters, ct);
    }

    private static string Truncate(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var trimmed = value.Trim();
        return trimmed.Length <= maxLength ? trimmed : trimmed[..maxLength];
    }
}

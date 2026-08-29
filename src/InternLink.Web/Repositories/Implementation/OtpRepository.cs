using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using InternLink.Web.Data;
using InternLink.Web.Models;
using InternLink.Web.Repositories.Interface;

namespace InternLink.Web.Repositories.Implementation;

public class OtpRepository : IOtpRepository
{
    private readonly ApplicationDbContext _db;

    public OtpRepository(ApplicationDbContext db)
    {
        _db = db;
    }

    public async Task InsertAsync(OtpCode code, CancellationToken ct = default)
    {
        var parameters = new[]
        {
            new SqlParameter("@id", SqlDbType.UniqueIdentifier) { Value = code.Id },
            new SqlParameter("@userId", SqlDbType.UniqueIdentifier) { Value = code.UserId },
            new SqlParameter("@codeHash", SqlDbType.NVarChar, 128) { Value = code.CodeHash },
            new SqlParameter("@expiresAt", SqlDbType.DateTimeOffset) { Value = code.ExpiresAt },
            new SqlParameter("@createdAt", SqlDbType.DateTimeOffset) { Value = code.CreatedAt },
            new SqlParameter("@lastSentAt", SqlDbType.DateTimeOffset) { Value = code.LastSentAt }
        };

        const string sql = @"
            INSERT INTO dbo.OtpCodes (Id, UserId, CodeHash, ExpiresAt, ConsumedAt, CreatedAt, LastSentAt)
            VALUES (@id, @userId, @codeHash, @expiresAt, NULL, @createdAt, @lastSentAt)";

        await _db.Database.ExecuteSqlRawAsync(sql, parameters, ct);
    }

    public async Task<OtpCode?> FindPendingByUserAsync(Guid userId, CancellationToken ct = default)
    {
        var userIdParam = new SqlParameter("@userId", SqlDbType.UniqueIdentifier) { Value = userId };
        const string sql = @"
            SELECT TOP(1) o.*
            FROM dbo.OtpCodes o
            WHERE o.UserId = @userId AND o.ConsumedAt IS NULL
            ORDER BY o.CreatedAt DESC";

        return await _db.OtpCodes
            .FromSqlRaw(sql, userIdParam)
            .AsNoTracking()
            .FirstOrDefaultAsync(ct);
    }

    public async Task ConsumeAsync(Guid id, DateTimeOffset consumedAt, CancellationToken ct = default)
    {
        var parameters = new[]
        {
            new SqlParameter("@id", SqlDbType.UniqueIdentifier) { Value = id },
            new SqlParameter("@consumedAt", SqlDbType.DateTimeOffset) { Value = consumedAt }
        };

        const string sql = @"
            UPDATE dbo.OtpCodes
            SET ConsumedAt = @consumedAt
            WHERE Id = @id AND ConsumedAt IS NULL";

        await _db.Database.ExecuteSqlRawAsync(sql, parameters, ct);
    }
}

using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using InternLink.Web.Data;
using InternLink.Web.Models;
using InternLink.Web.Repositories.Interface;

namespace InternLink.Web.Repositories.Implementation;

public class CompanyRepository : ICompanyRepository
{
    private readonly ApplicationDbContext _db;

    public CompanyRepository(ApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<Company?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var idParam = new SqlParameter("@id", SqlDbType.UniqueIdentifier) { Value = id };
        const string sql = "SELECT c.* FROM dbo.Companies c WHERE c.Id = @id";

        return await _db.Companies
            .FromSqlRaw(sql, idParam)
            .AsNoTracking()
            .FirstOrDefaultAsync(ct);
    }

    public async Task<Company?> GetByUserIdAsync(Guid userId, CancellationToken ct = default)
    {
        var userIdParam = new SqlParameter("@userId", SqlDbType.UniqueIdentifier) { Value = userId };
        const string sql = "SELECT c.* FROM dbo.Companies c WHERE c.UserId = @userId";

        return await _db.Companies
            .FromSqlRaw(sql, userIdParam)
            .AsNoTracking()
            .FirstOrDefaultAsync(ct);
    }
}

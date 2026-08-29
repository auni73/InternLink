using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using InternLink.Web.Data;
using InternLink.Web.Models;
using InternLink.Web.Models.Enums;
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

    public async Task UpdateProfileAsync(Company company, CancellationToken ct = default)
    {
        var idParam = new SqlParameter("@id", SqlDbType.UniqueIdentifier) { Value = company.Id };
        var nameParam = new SqlParameter("@name", SqlDbType.NVarChar, 200) { Value = company.CompanyName.Trim() };
        var websiteParam = new SqlParameter("@website", SqlDbType.NVarChar, 500) 
        { 
            Value = string.IsNullOrWhiteSpace(company.CorporateWebsite) ? DBNull.Value : company.CorporateWebsite.Trim() 
        };
        var sectorParam = new SqlParameter("@sector", SqlDbType.NVarChar, 100) { Value = company.IndustrySector.Trim() };

        const string sql = @"
            UPDATE dbo.Companies 
            SET CompanyName = @name, 
                CorporateWebsite = @website, 
                IndustrySector = @sector 
            WHERE Id = @id";

        await _db.Database.ExecuteSqlRawAsync(sql, new object[] { idParam, nameParam, websiteParam, sectorParam }, ct);
    }

    public async Task<VerificationStatus?> GetVerificationStatusAsync(Guid companyId, CancellationToken ct = default)
    {
        var idParam = new SqlParameter("@id", SqlDbType.UniqueIdentifier) { Value = companyId };
        const string sql = "SELECT CAST(c.VerificationStatus AS int) AS Value FROM dbo.Companies c WHERE c.Id = @id";

        var val = await _db.Database
            .SqlQueryRaw<int?>(sql, idParam)
            .FirstOrDefaultAsync(ct);

        return val.HasValue ? (VerificationStatus)val.Value : null;
    }
}

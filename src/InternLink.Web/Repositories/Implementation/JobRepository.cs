using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using InternLink.Web.Data;
using InternLink.Web.Helpers;
using InternLink.Web.Models;
using InternLink.Web.Models.Enums;
using InternLink.Web.Repositories.Interface;
using InternLink.Web.ViewModels;

namespace InternLink.Web.Repositories.Implementation;

public class JobRepository : IJobRepository
{
    private readonly ApplicationDbContext _db;

    public JobRepository(ApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<Job>> GetApprovedOpenJobsAsync(
        LocationType? locationType, 
        int page, 
        int pageSize, 
        CancellationToken ct = default)
    {
        var offset = Math.Max(0, (page - 1) * pageSize);
        var locationTypeParam = new SqlParameter("@lt", SqlDbType.TinyInt)
        {
            Value = locationType.HasValue ? (object)(byte)locationType.Value : DBNull.Value
        };
        var offsetParam = new SqlParameter("@offset", SqlDbType.Int) { Value = offset };
        var pageSizeParam = new SqlParameter("@pageSize", SqlDbType.Int) { Value = pageSize };

        const string sql = @"
            SELECT j.* 
            FROM dbo.Jobs j 
            WHERE j.IsApproved = 1 
              AND j.IsClosed = 0 
              AND j.DeadLine >= SYSDATETIMEOFFSET() 
              AND (@lt IS NULL OR j.LocationType = @lt) 
            ORDER BY j.DeadLine ASC 
            OFFSET @offset ROWS FETCH NEXT @pageSize ROWS ONLY";

        return await _db.Jobs
            .FromSqlRaw(sql, locationTypeParam, offsetParam, pageSizeParam)
            .AsNoTracking()
            .ToListAsync(ct);
    }

    public async Task<Job?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var idParam = new SqlParameter("@id", SqlDbType.UniqueIdentifier) { Value = id };
        const string sql = "SELECT j.* FROM dbo.Jobs j WHERE j.Id = @id";

        return await _db.Jobs
            .FromSqlRaw(sql, idParam)
            .AsNoTracking()
            .FirstOrDefaultAsync(ct);
    }

    public async Task<int> GetApprovedOpenJobsCountAsync(LocationType? locationType, CancellationToken ct = default)
    {
        var locationTypeParam = new SqlParameter("@lt", SqlDbType.TinyInt)
        {
            Value = locationType.HasValue ? (object)(byte)locationType.Value : DBNull.Value
        };

        const string sql = @"
            SELECT COUNT(*) AS Value 
            FROM dbo.Jobs j 
            WHERE j.IsApproved = 1 
              AND j.IsClosed = 0 
              AND j.DeadLine >= SYSDATETIMEOFFSET() 
              AND (@lt IS NULL OR j.LocationType = @lt)";

        return await _db.Database
            .SqlQueryRaw<int>(sql, locationTypeParam)
            .FirstOrDefaultAsync(ct);
    }
    public async Task<(IReadOnlyList<JobListItemViewModel> Items, int TotalCount)> SearchApprovedOpenJobsAsync(
        JobSearchFilter filter, 
        Guid? studentId, 
        bool isFtsAvailable, 
        CancellationToken ct = default)
    {
        var offset = Math.Max(0, (filter.Page - 1) * filter.PageSize);
        var ftsQuery = isFtsAvailable ? FtsQueryBuilder.BuildPrefixAndQuery(filter.Keyword) : null;
        var useFts = !string.IsNullOrWhiteSpace(ftsQuery);
        var useLike = !useFts && !string.IsNullOrWhiteSpace(filter.Keyword);

        var parameters = new List<SqlParameter>
        {
            new("@lt", SqlDbType.TinyInt)
            {
                Value = filter.LocationType.HasValue ? (object)(byte)filter.LocationType.Value : DBNull.Value
            },
            new("@studentId", SqlDbType.UniqueIdentifier)
            {
                Value = studentId.HasValue ? (object)studentId.Value : DBNull.Value
            },
            new("@offset", SqlDbType.Int) { Value = offset },
            new("@pageSize", SqlDbType.Int) { Value = filter.PageSize }
        };

        var whereClause = @"
            WHERE j.IsApproved = 1 
              AND j.IsClosed = 0 
              AND j.DeadLine >= SYSDATETIMEOFFSET() 
              AND (@lt IS NULL OR j.LocationType = @lt)";

        if (filter.RelevantToMe && studentId.HasValue)
        {
            whereClause += @"
              AND EXISTS (
                  SELECT 1 
                  FROM dbo.JobSkills js 
                  INNER JOIN dbo.StudentSkills ss ON js.SkillId = ss.SkillId 
                  WHERE js.JobId = j.Id AND ss.StudentId = @studentId)";
        }

        var joinClause = "INNER JOIN dbo.Companies c ON j.CompanyId = c.Id";
        var orderByClause = "ORDER BY j.DeadLine ASC";

        if (useFts)
        {
            parameters.Add(new SqlParameter("@ftsQuery", SqlDbType.NVarChar, 500) { Value = ftsQuery });
            joinClause += " INNER JOIN CONTAINSTABLE(dbo.Jobs, (Title, CoreDescription), @ftsQuery) AS fts ON j.Id = fts.[KEY]";
            orderByClause = "ORDER BY fts.RANK DESC, j.DeadLine ASC";
        }
        else if (useLike)
        {
            var term = $"%{filter.Keyword!.Trim()}%";
            parameters.Add(new SqlParameter("@kw", SqlDbType.NVarChar, 200) { Value = term });
            whereClause += " AND (j.Title LIKE @kw OR j.CoreDescription LIKE @kw)";
        }

        // Count Query
        var countSql = $@"
            SELECT COUNT(*) AS Value 
            FROM dbo.Jobs j 
            {joinClause} 
            {whereClause}";

        var countParams = parameters
            .Where(p => p.ParameterName != "@offset" && p.ParameterName != "@pageSize")
            .Select(p => (SqlParameter)((ICloneable)p).Clone())
            .ToArray();

        var totalCount = await _db.Database
            .SqlQueryRaw<int>(countSql, countParams)
            .FirstOrDefaultAsync(ct);

        if (totalCount == 0)
        {
            return (Array.Empty<JobListItemViewModel>(), 0);
        }

        // Data Query
        var dataSql = $@"
            SELECT 
                j.Id,
                j.Title,
                c.CompanyName,
                c.IndustrySector,
                j.LocationType,
                j.DeadLine,
                CAST(CASE WHEN @studentId IS NOT NULL AND EXISTS (
                    SELECT 1 FROM dbo.Applications a WHERE a.JobId = j.Id AND a.StudentId = @studentId
                ) THEN 1 ELSE 0 END AS bit) AS HasApplied
            FROM dbo.Jobs j
            {joinClause}
            {whereClause}
            {orderByClause}
            OFFSET @offset ROWS FETCH NEXT @pageSize ROWS ONLY";

        var rawRows = await _db.Database
            .SqlQueryRaw<JobSearchRowResult>(dataSql, parameters.ToArray())
            .ToListAsync(ct);

        if (rawRows.Count == 0)
        {
            return (Array.Empty<JobListItemViewModel>(), totalCount);
        }

        // Fetch skills for the retrieved jobs in one query
        var jobIds = rawRows.Select(r => r.Id).ToList();
        var jobSkills = await _db.JobSkills
            .Where(js => jobIds.Contains(js.JobId))
            .Include(js => js.Skill)
            .AsNoTracking()
            .ToListAsync(ct);

        var skillLookup = jobSkills
            .GroupBy(js => js.JobId)
            .ToDictionary(
                g => g.Key, 
                g => g.Select(js => new JobSkillBadgeViewModel
                {
                    SkillId = js.SkillId,
                    SkillName = js.Skill.SkillName,
                    ImportanceWeight = js.RequiredImportanceWeight
                }).ToList());

        var items = rawRows.Select(r => new JobListItemViewModel
        {
            Id = r.Id,
            Title = r.Title,
            CompanyName = r.CompanyName,
            IndustrySector = r.IndustrySector,
            LocationType = (LocationType)r.LocationType,
            Deadline = r.DeadLine,
            HasApplied = r.HasApplied,
            RequiredSkills = skillLookup.TryGetValue(r.Id, out var sk) ? sk : new List<JobSkillBadgeViewModel>()
        }).ToList();

        return (items, totalCount);
    }

    public async Task<JobDetailViewModel?> GetApprovedJobDetailAsync(Guid id, Guid? studentId, CancellationToken ct = default)
    {
        var idParam = new SqlParameter("@id", SqlDbType.UniqueIdentifier) { Value = id };
        var studentIdParam = new SqlParameter("@studentId", SqlDbType.UniqueIdentifier) 
        { 
            Value = studentId.HasValue ? (object)studentId.Value : DBNull.Value 
        };

        const string sql = @"
            SELECT 
                j.Id,
                j.CompanyId,
                j.Title,
                c.CompanyName,
                c.CorporateWebsite,
                c.IndustrySector,
                j.LocationType,
                j.DeadLine,
                j.CoreDescription,
                j.SelectionCriteria,
                CAST(CASE WHEN @studentId IS NOT NULL AND EXISTS (
                    SELECT 1 FROM dbo.Applications a WHERE a.JobId = j.Id AND a.StudentId = @studentId
                ) THEN 1 ELSE 0 END AS bit) AS HasApplied
            FROM dbo.Jobs j
            INNER JOIN dbo.Companies c ON j.CompanyId = c.Id
            WHERE j.Id = @id 
              AND j.IsApproved = 1 
              AND j.IsClosed = 0 
              AND j.DeadLine >= SYSDATETIMEOFFSET()";

        var detailRow = await _db.Database
            .SqlQueryRaw<JobDetailRowResult>(sql, idParam, studentIdParam)
            .FirstOrDefaultAsync(ct);

        if (detailRow is null)
        {
            return null;
        }

        var skills = await _db.JobSkills
            .Where(js => js.JobId == id)
            .Include(js => js.Skill)
            .OrderByDescending(js => js.RequiredImportanceWeight)
            .AsNoTracking()
            .Select(js => new JobSkillBadgeViewModel
            {
                SkillId = js.SkillId,
                SkillName = js.Skill.SkillName,
                ImportanceWeight = js.RequiredImportanceWeight
            })
            .ToListAsync(ct);

        return new JobDetailViewModel
        {
            Id = detailRow.Id,
            CompanyId = detailRow.CompanyId,
            Title = detailRow.Title,
            CompanyName = detailRow.CompanyName,
            CorporateWebsite = detailRow.CorporateWebsite,
            IndustrySector = detailRow.IndustrySector,
            LocationType = (LocationType)detailRow.LocationType,
            Deadline = detailRow.DeadLine,
            CoreDescription = detailRow.CoreDescription,
            SelectionCriteria = detailRow.SelectionCriteria,
            HasApplied = detailRow.HasApplied,
            RequiredSkills = skills
        };
    }

    public async Task<IReadOnlyList<CompanyJobListItemViewModel>> GetCompanyJobsAsync(Guid companyId, CancellationToken ct = default)
    {
        var companyIdParam = new SqlParameter("@companyId", SqlDbType.UniqueIdentifier) { Value = companyId };

        const string sql = @"
            SELECT 
                j.Id AS JobId,
                j.Title,
                j.LocationType,
                j.DeadLine,
                j.IsApproved,
                j.IsClosed,
                j.CreatedAt,
                (SELECT COUNT(1) FROM dbo.Applications a WHERE a.JobId = j.Id) AS ApplicantCount
            FROM dbo.Jobs j
            WHERE j.CompanyId = @companyId
            ORDER BY j.CreatedAt DESC";

        var rows = await _db.Database
            .SqlQueryRaw<CompanyJobRowResult>(sql, companyIdParam)
            .ToListAsync(ct);

        return rows.Select(r => new CompanyJobListItemViewModel
        {
            JobId = r.JobId,
            Title = r.Title,
            LocationType = (LocationType)r.LocationType,
            DeadLine = r.DeadLine,
            IsApproved = r.IsApproved,
            IsClosed = r.IsClosed,
            CreatedAt = r.CreatedAt,
            ApplicantCount = r.ApplicantCount
        }).ToList();
    }

    public async Task<CompanyJobEditViewModel?> GetCompanyJobForEditAsync(Guid jobId, Guid companyId, CancellationToken ct = default)
    {
        var jobIdParam = new SqlParameter("@jobId", SqlDbType.UniqueIdentifier) { Value = jobId };
        var companyIdParam = new SqlParameter("@companyId", SqlDbType.UniqueIdentifier) { Value = companyId };

        const string jobSql = @"
            SELECT j.* 
            FROM dbo.Jobs j 
            WHERE j.Id = @jobId AND j.CompanyId = @companyId";

        var job = await _db.Jobs
            .FromSqlRaw(jobSql, jobIdParam, companyIdParam)
            .AsNoTracking()
            .FirstOrDefaultAsync(ct);

        if (job is null)
        {
            return null;
        }

        const string skillsSql = @"
            SELECT js.SkillId, js.RequiredImportanceWeight AS Weight, s.SkillName
            FROM dbo.JobSkills js
            INNER JOIN dbo.Skills s ON js.SkillId = s.Id
            WHERE js.JobId = @jobId
            ORDER BY js.RequiredImportanceWeight DESC, s.SkillName ASC";

        var selectedSkills = await _db.Database
            .SqlQueryRaw<JobSkillWeightRowResult>(skillsSql, new SqlParameter("@jobId", SqlDbType.UniqueIdentifier) { Value = jobId })
            .ToListAsync(ct);

        return new CompanyJobEditViewModel
        {
            Id = job.Id,
            Title = job.Title,
            CoreDescription = job.CoreDescription,
            SelectionCriteria = job.SelectionCriteria,
            LocationType = job.LocationType,
            DeadLineDate = job.DeadLine.DateTime.Date,
            IsApproved = job.IsApproved,
            IsClosed = job.IsClosed,
            SelectedSkills = selectedSkills.Select(s => new JobSkillWeightDto
            {
                SkillId = s.SkillId,
                SkillName = s.SkillName,
                Weight = s.Weight
            }).ToList()
        };
    }

    public async Task<Guid> CreateJobWithSkillsAsync(Guid companyId, CompanyJobEditViewModel model, CancellationToken ct = default)
    {
        var jobId = Guid.NewGuid();
        var deadlineOffset = new DateTimeOffset(model.DeadLineDate.Date.AddDays(1).AddTicks(-1), TimeSpan.Zero);

        await using var transaction = await _db.Database.BeginTransactionAsync(ct);
        try
        {
            const string insertJobSql = @"
                INSERT INTO dbo.Jobs (Id, CompanyId, Title, CoreDescription, SelectionCriteria, LocationType, DeadLine, IsApproved, IsClosed, CreatedAt)
                VALUES (@id, @companyId, @title, @desc, @crit, @loc, @deadline, 0, 0, SYSDATETIMEOFFSET())";

            await _db.Database.ExecuteSqlRawAsync(insertJobSql, new object[] {
                new SqlParameter("@id", SqlDbType.UniqueIdentifier) { Value = jobId },
                new SqlParameter("@companyId", SqlDbType.UniqueIdentifier) { Value = companyId },
                new SqlParameter("@title", SqlDbType.NVarChar, 200) { Value = model.Title.Trim() },
                new SqlParameter("@desc", SqlDbType.NVarChar, -1) { Value = model.CoreDescription.Trim() },
                new SqlParameter("@crit", SqlDbType.NVarChar, -1) { Value = model.SelectionCriteria?.Trim() ?? string.Empty },
                new SqlParameter("@loc", SqlDbType.TinyInt) { Value = (byte)model.LocationType },
                new SqlParameter("@deadline", SqlDbType.DateTimeOffset) { Value = deadlineOffset }
            }, ct);

            if (model.SelectedSkills != null && model.SelectedSkills.Count > 0)
            {
                foreach (var skill in model.SelectedSkills)
                {
                    const string insertSkillSql = @"
                        INSERT INTO dbo.JobSkills (JobId, SkillId, RequiredImportanceWeight)
                        VALUES (@jobId, @skillId, @weight)";

                    await _db.Database.ExecuteSqlRawAsync(insertSkillSql, new object[] {
                        new SqlParameter("@jobId", SqlDbType.UniqueIdentifier) { Value = jobId },
                        new SqlParameter("@skillId", SqlDbType.UniqueIdentifier) { Value = skill.SkillId },
                        new SqlParameter("@weight", SqlDbType.TinyInt) { Value = Math.Clamp(skill.Weight, (byte)1, (byte)5) }
                    }, ct);
                }
            }

            await transaction.CommitAsync(ct);
            return jobId;
        }
        catch
        {
            await transaction.RollbackAsync(ct);
            throw;
        }
    }

    public async Task<bool> UpdateJobWithSkillsAsync(Guid jobId, Guid companyId, CompanyJobEditViewModel model, CancellationToken ct = default)
    {
        const string checkSql = "SELECT COUNT(1) AS Value FROM dbo.Jobs j WHERE j.Id = @jobId AND j.CompanyId = @companyId";
        var exists = await _db.Database.SqlQueryRaw<int>(checkSql,
            new SqlParameter("@jobId", SqlDbType.UniqueIdentifier) { Value = jobId },
            new SqlParameter("@companyId", SqlDbType.UniqueIdentifier) { Value = companyId }
        ).FirstOrDefaultAsync(ct);

        if (exists == 0)
        {
            return false;
        }

        var deadlineOffset = new DateTimeOffset(model.DeadLineDate.Date.AddDays(1).AddTicks(-1), TimeSpan.Zero);

        await using var transaction = await _db.Database.BeginTransactionAsync(ct);
        try
        {
            // Architectural invariant: Keep IsApproved unchanged on edit.
            // Re-queue for administrative approval on major structural edits is noted as an optional extension.
            const string updateJobSql = @"
                UPDATE dbo.Jobs
                SET Title = @title,
                    CoreDescription = @desc,
                    SelectionCriteria = @crit,
                    LocationType = @loc,
                    DeadLine = @deadline
                WHERE Id = @jobId AND CompanyId = @companyId";

            await _db.Database.ExecuteSqlRawAsync(updateJobSql, new object[] {
                new SqlParameter("@jobId", SqlDbType.UniqueIdentifier) { Value = jobId },
                new SqlParameter("@companyId", SqlDbType.UniqueIdentifier) { Value = companyId },
                new SqlParameter("@title", SqlDbType.NVarChar, 200) { Value = model.Title.Trim() },
                new SqlParameter("@desc", SqlDbType.NVarChar, -1) { Value = model.CoreDescription.Trim() },
                new SqlParameter("@crit", SqlDbType.NVarChar, -1) { Value = model.SelectionCriteria?.Trim() ?? string.Empty },
                new SqlParameter("@loc", SqlDbType.TinyInt) { Value = (byte)model.LocationType },
                new SqlParameter("@deadline", SqlDbType.DateTimeOffset) { Value = deadlineOffset }
            }, ct);

            const string deleteSkillsSql = "DELETE FROM dbo.JobSkills WHERE JobId = @jobId";
            await _db.Database.ExecuteSqlRawAsync(deleteSkillsSql, new object[] {
                new SqlParameter("@jobId", SqlDbType.UniqueIdentifier) { Value = jobId }
            }, ct);

            if (model.SelectedSkills != null && model.SelectedSkills.Count > 0)
            {
                foreach (var skill in model.SelectedSkills)
                {
                    const string insertSkillSql = @"
                        INSERT INTO dbo.JobSkills (JobId, SkillId, RequiredImportanceWeight)
                        VALUES (@jobId, @skillId, @weight)";

                    await _db.Database.ExecuteSqlRawAsync(insertSkillSql, new object[] {
                        new SqlParameter("@jobId", SqlDbType.UniqueIdentifier) { Value = jobId },
                        new SqlParameter("@skillId", SqlDbType.UniqueIdentifier) { Value = skill.SkillId },
                        new SqlParameter("@weight", SqlDbType.TinyInt) { Value = Math.Clamp(skill.Weight, (byte)1, (byte)5) }
                    }, ct);
                }
            }

            await transaction.CommitAsync(ct);
            return true;
        }
        catch
        {
            await transaction.RollbackAsync(ct);
            throw;
        }
    }

    public async Task<bool> CloseJobAsync(Guid jobId, Guid companyId, CancellationToken ct = default)
    {
        // Parameterized update to set IsClosed = 1. Strictly no deletion endpoint per project rules.
        const string closeSql = "UPDATE dbo.Jobs SET IsClosed = 1 WHERE Id = @jobId AND CompanyId = @companyId";
        var rows = await _db.Database.ExecuteSqlRawAsync(closeSql, new object[] {
            new SqlParameter("@jobId", SqlDbType.UniqueIdentifier) { Value = jobId },
            new SqlParameter("@companyId", SqlDbType.UniqueIdentifier) { Value = companyId }
        }, ct);

        return rows > 0;
    }
}

// Helper POCOs for SqlQuery mapping
public class JobSearchRowResult
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string CompanyName { get; set; } = string.Empty;
    public string? IndustrySector { get; set; }
    public byte LocationType { get; set; }
    public DateTimeOffset DeadLine { get; set; }
    public bool HasApplied { get; set; }
}

public class JobDetailRowResult
{
    public Guid Id { get; set; }
    public Guid CompanyId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string CompanyName { get; set; } = string.Empty;
    public string? CorporateWebsite { get; set; }
    public string IndustrySector { get; set; } = string.Empty;
    public byte LocationType { get; set; }
    public DateTimeOffset DeadLine { get; set; }
    public string CoreDescription { get; set; } = string.Empty;
    public string SelectionCriteria { get; set; } = string.Empty;
    public bool HasApplied { get; set; }
}

public class CompanyJobRowResult
{
    public Guid JobId { get; set; }
    public string Title { get; set; } = string.Empty;
    public byte LocationType { get; set; }
    public DateTimeOffset DeadLine { get; set; }
    public bool IsApproved { get; set; }
    public bool IsClosed { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public int ApplicantCount { get; set; }
}

public class JobSkillWeightRowResult
{
    public Guid SkillId { get; set; }
    public string SkillName { get; set; } = string.Empty;
    public byte Weight { get; set; }
}

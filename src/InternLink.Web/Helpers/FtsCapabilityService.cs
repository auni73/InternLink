using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using InternLink.Web.Data;

namespace InternLink.Web.Helpers;

public interface IFtsCapabilityService
{
    Task<bool> IsFtsAvailableAsync(CancellationToken ct = default);
}

public class FtsCapabilityService : IFtsCapabilityService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<FtsCapabilityService> _logger;
    private bool? _cachedCapability;
    private readonly SemaphoreSlim _lock = new(1, 1);

    public FtsCapabilityService(IServiceProvider serviceProvider, ILogger<FtsCapabilityService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    public async Task<bool> IsFtsAvailableAsync(CancellationToken ct = default)
    {
        if (_cachedCapability.HasValue)
        {
            return _cachedCapability.Value;
        }

        await _lock.WaitAsync(ct);
        try
        {
            if (_cachedCapability.HasValue)
            {
                return _cachedCapability.Value;
            }

            using var scope = _serviceProvider.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            try
            {
                // Check if full-text engine is installed
                var isInstalled = await db.Database
                    .SqlQueryRaw<int?>("SELECT CAST(SERVERPROPERTY('IsFullTextInstalled') AS int) AS Value")
                    .FirstOrDefaultAsync(ct);

                if (isInstalled != 1)
                {
                    _logger.LogWarning("SQL Server Full-Text Search is NOT installed on this instance. Falling back to parameterized LIKE search.");
                    _cachedCapability = false;
                    return false;
                }

                // Check if the FTS catalog / index exists on Jobs table
                var hasIndex = await db.Database
                    .SqlQueryRaw<int>(@"
                        SELECT COUNT(*) AS Value 
                        FROM sys.fulltext_indexes fi
                        INNER JOIN sys.objects o ON fi.object_id = o.object_id
                        WHERE o.name = 'Jobs'")
                    .FirstOrDefaultAsync(ct);

                if (hasIndex > 0)
                {
                    _logger.LogInformation("SQL Server Full-Text Search catalog and index detected on Jobs table. FTS search active.");
                    _cachedCapability = true;
                    return true;
                }
                else
                {
                    _logger.LogWarning("Full-Text Search is installed but no FTS index found on Jobs table. Falling back to LIKE search.");
                    _cachedCapability = false;
                    return false;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to probe SQL Server FTS capability. Degrading to LIKE search.");
                _cachedCapability = false;
                return false;
            }
        }
        finally
        {
            _lock.Release();
        }
    }
}

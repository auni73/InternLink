using InternLink.Web.ViewModels;

namespace InternLink.Web.Services.Jobs;

public interface IExternalJobIngestionService
{
    Task<ExternalJobSyncResultDto> SyncAllSourcesAsync(CancellationToken ct = default);
    Task<ExternalJobSourceSyncStatsDto> SyncArbeitnowAsync(CancellationToken ct = default);
    Task<ExternalJobSourceSyncStatsDto> SyncBdJobsAsync(CancellationToken ct = default);
}

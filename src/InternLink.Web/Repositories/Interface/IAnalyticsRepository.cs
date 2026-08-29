using InternLink.Web.ViewModels;

namespace InternLink.Web.Repositories.Interface;

public interface IAnalyticsRepository
{
    Task<AdminAnalyticsViewModel> GetAdminAnalyticsAsync(CancellationToken ct = default);
}

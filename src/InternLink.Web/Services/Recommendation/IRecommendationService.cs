using InternLink.Web.ViewModels;

namespace InternLink.Web.Services.Recommendation;

public interface IRecommendationService
{
    Task<RecommendationResultViewModel> GetRecommendedJobsAsync(Guid studentId, CancellationToken ct = default);
}

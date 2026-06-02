using GuidYu_API.DTOs;

namespace GuidYu_API.Services;

public interface IDashboardService
{
    Task<DashboardOverviewDto?> GetOverviewAsync(int userId);
    Task<IEnumerable<DashboardMetricDto>> GetMetricsAsync(int userId);
    Task<IEnumerable<DashboardProgressDto>> GetProgressAsync(int userId);
    Task<IEnumerable<DashboardNextStepDto>> GetNextStepsAsync(int userId);
    Task<IEnumerable<DashboardInsightDto>> GetInsightsAsync(int userId);
    Task<IEnumerable<DashboardRoadmapDto>> GetRoadmapAsync(int userId);
    Task<IEnumerable<DashboardActivityDto>> GetActivityAsync(int userId);
}

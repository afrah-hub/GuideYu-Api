using GuidYu_API.DTOs;
using GuidYu_API.Repositories;

namespace GuidYu_API.Services;

public class DashboardService : IDashboardService
{
    private readonly IDashboardRepository _dashboardRepository;

    public DashboardService(IDashboardRepository dashboardRepository)
    {
        _dashboardRepository = dashboardRepository;
    }

    public async Task<DashboardOverviewDto?> GetOverviewAsync(int userId) => await _dashboardRepository.GetOverviewAsync(userId);
    public async Task<IEnumerable<DashboardMetricDto>> GetMetricsAsync(int userId) => await _dashboardRepository.GetMetricsAsync(userId);
    public async Task<IEnumerable<DashboardProgressDto>> GetProgressAsync(int userId) => await _dashboardRepository.GetProgressAsync(userId);
    public async Task<IEnumerable<DashboardNextStepDto>> GetNextStepsAsync(int userId) => await _dashboardRepository.GetNextStepsAsync(userId);
    public async Task<IEnumerable<DashboardInsightDto>> GetInsightsAsync(int userId) => await _dashboardRepository.GetInsightsAsync(userId);
    public async Task<IEnumerable<DashboardRoadmapDto>> GetRoadmapAsync(int userId) => await _dashboardRepository.GetRoadmapAsync(userId);
    public async Task<IEnumerable<DashboardActivityDto>> GetActivityAsync(int userId) => await _dashboardRepository.GetActivityAsync(userId);
}

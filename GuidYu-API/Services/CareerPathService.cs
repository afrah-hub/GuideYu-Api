using System.Text.Json;
using GuidYu_API.DTOs;
using GuidYu_API.Repositories;
using System.Collections.Generic;

namespace GuidYu_API.Services;

public class CareerPathService : ICareerPathService
{
    private readonly ICareerPathRepository _careerPathRepository;
    private readonly IUserRepository _userRepository;
    private readonly IAiCurriculumGenerator _aiCurriculumGenerator;

    public CareerPathService(ICareerPathRepository careerPathRepository, IUserRepository userRepository, IAiCurriculumGenerator aiCurriculumGenerator)
    {
        _careerPathRepository = careerPathRepository;
        _userRepository = userRepository;
        _aiCurriculumGenerator = aiCurriculumGenerator;
    }

    public async Task<CareerPathOverviewDto> GetCareerPathOverviewAsync(int userId, string? targetCareer = null, string? stage = "Beginner")
    {
        // If targetCareer is null or empty, attempt to resolve it from the user's profile CareerGoal
        if (string.IsNullOrWhiteSpace(targetCareer))
        {
            var user = await _userRepository.GetUserByIdAsync(userId);
            targetCareer = user?.CareerGoal;
        }

        // First, check if we already have a saved career path in the database
        var repoOverview = await _careerPathRepository.GetCareerPathOverviewAsync(userId, targetCareer);
        
        // If we have a saved one, and it matches the requested career (or no specific career is requested), reuse it!
        // We only reuse it if it is a fully populated career path containing journey steps;
        // if it is an empty shell, we proceed to generate the fully populated roadmap and save it.
        if (repoOverview != null && repoOverview.Summary != null && repoOverview.Journey != null && repoOverview.Journey.Any() &&
            (string.IsNullOrWhiteSpace(targetCareer) || string.Equals(repoOverview.Summary.TargetRole, targetCareer, StringComparison.OrdinalIgnoreCase)))
        {
            return repoOverview;
        }

        // If not found or if the career doesn't match, load the seeded/mapped database roadmap or fallback instantly
        if (!string.IsNullOrWhiteSpace(targetCareer))
        {
            try
            {
                var seededOverview = await _careerPathRepository.GetSeededCareerPathOverviewAsync(userId, targetCareer);
                if (seededOverview != null)
                {
                    return seededOverview;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error retrieving seeded career path: {ex.Message}");
            }
        }

        // If we have any repoOverview saved (even if career didn't match, but targetCareer wasn't supplied), return it as fallback
        if (repoOverview != null)
        {
            return repoOverview;
        }

        // No saved overview; return empty overview to avoid static modules
        var emptyOverviewFallback = new CareerPathOverviewDto
        {
            Summary = new CareerPathSummaryDto
            {
                CurrentRole = "Professional",
                TargetRole = targetCareer ?? "Target Role",
                MatchPercentage = 0,
                EstimatedTime = "0"
            },
            Journey = new List<CareerPathStepDto>(),
            Skills = new List<CareerPathSkillDto>(),
            Insights = new List<CareerPathInsightDto>()
        };
        return emptyOverviewFallback;
    }

    public async Task<bool> SaveCareerPathAsync(int userId, CareerPathOverviewDto data)
    {
        return await _careerPathRepository.SaveCareerPathAsync(userId, data);
    }
}

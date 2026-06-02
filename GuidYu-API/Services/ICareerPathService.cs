using GuidYu_API.DTOs;

namespace GuidYu_API.Services;

public interface ICareerPathService
{
    Task<CareerPathOverviewDto> GetCareerPathOverviewAsync(int userId, string? targetCareer = null, string? stage = "Beginner");
    Task<bool> SaveCareerPathAsync(int userId, CareerPathOverviewDto data);
}

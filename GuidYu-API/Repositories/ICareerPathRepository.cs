using GuidYu_API.DTOs;

namespace GuidYu_API.Repositories;

public interface ICareerPathRepository
{
    Task<CareerPathOverviewDto> GetCareerPathOverviewAsync(int userId, string? targetCareer = null);
    Task<bool> SaveCareerPathAsync(int userId, CareerPathOverviewDto data);
    Task<CareerPathOverviewDto> GetSeededCareerPathOverviewAsync(int userId, string targetCareer);
}

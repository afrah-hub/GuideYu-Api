using GuidYu_API.DTOs;

namespace GuidYu_API.Services;

public interface IResumeService
{
    Task<ResumeDto?> GetMyResumeAsync(int userId);
    Task<ResumeDto> CreateResumeAsync(int userId, CreateResumeDto createDto);
    Task<ResumeDto?> UpdateResumeAsync(int userId, int resumeId, UpdateResumeDto updateDto);
    Task<bool> DeleteResumeAsync(int userId, int resumeId);
    Task<byte[]> GeneratePdfAsync(int userId);
}

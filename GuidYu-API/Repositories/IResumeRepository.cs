using GuidYu_API.Models;

namespace GuidYu_API.Repositories;

public interface IResumeRepository
{
    Task<Resume?> GetResumeByUserIdAsync(int userId);
    Task<Resume?> GetResumeByIdAsync(int id);
    Task<Resume> CreateResumeAsync(Resume resume);
    Task<Resume> UpdateResumeAsync(Resume resume);
    Task<bool> DeleteResumeAsync(int id);
}

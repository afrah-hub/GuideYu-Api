using GuidYu_API.Data;
using GuidYu_API.Models;
using Microsoft.EntityFrameworkCore;

namespace GuidYu_API.Repositories;

public class ResumeRepository : IResumeRepository
{
    private readonly GuidYuDbContext _context;

    public ResumeRepository(GuidYuDbContext context)
    {
        _context = context;
    }

    public async Task<Resume?> GetResumeByUserIdAsync(int userId)
    {
        return await _context.Resumes
            .FirstOrDefaultAsync(r => r.UserId == userId);
    }

    public async Task<Resume?> GetResumeByIdAsync(int id)
    {
        return await _context.Resumes.FindAsync(id);
    }

    public async Task<Resume> CreateResumeAsync(Resume resume)
    {
        _context.Resumes.Add(resume);
        await _context.SaveChangesAsync();
        return resume;
    }

    public async Task<Resume> UpdateResumeAsync(Resume resume)
    {
        resume.UpdatedAt = DateTime.UtcNow;
        _context.Resumes.Update(resume);
        await _context.SaveChangesAsync();
        return resume;
    }

    public async Task<bool> DeleteResumeAsync(int id)
    {
        var resume = await _context.Resumes.FindAsync(id);
        if (resume == null) return false;

        _context.Resumes.Remove(resume);
        await _context.SaveChangesAsync();
        return true;
    }
}

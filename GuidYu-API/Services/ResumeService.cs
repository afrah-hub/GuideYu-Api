using GuidYu_API.DTOs;
using GuidYu_API.Models;
using GuidYu_API.Repositories;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace GuidYu_API.Services;

public class ResumeService : IResumeService
{
    private readonly IResumeRepository _resumeRepository;

    public ResumeService(IResumeRepository resumeRepository)
    {
        _resumeRepository = resumeRepository;
    }

    public async Task<ResumeDto?> GetMyResumeAsync(int userId)
    {
        var resume = await _resumeRepository.GetResumeByUserIdAsync(userId);
        if (resume == null) return null;

        return MapToDto(resume);
    }

    public async Task<ResumeDto> CreateResumeAsync(int userId, CreateResumeDto createDto)
    {
        var resume = new Resume
        {
            UserId = userId,
            CareerId = createDto.CareerId,
            FullName = createDto.FullName,
            Email = createDto.Email,
            PhoneNumber = createDto.PhoneNumber,
            Location = createDto.Location,
            ProfessionalSummary = createDto.ProfessionalSummary,
            Skills = createDto.Skills,
            Education = createDto.Education,
            Projects = createDto.Projects,
            Experience = createDto.Experience,
            Certifications = createDto.Certifications,
            GithubUrl = createDto.GithubUrl,
            LinkedInUrl = createDto.LinkedinUrl,
            PortfolioUrl = createDto.PortfolioUrl
        };

        var createdResume = await _resumeRepository.CreateResumeAsync(resume);
        return MapToDto(createdResume);
    }

    public async Task<ResumeDto?> UpdateResumeAsync(int userId, int resumeId, UpdateResumeDto updateDto)
    {
        var resume = await _resumeRepository.GetResumeByIdAsync(resumeId);
        if (resume == null || resume.UserId != userId) return null;

        resume.CareerId = updateDto.CareerId;
        resume.FullName = updateDto.FullName;
        resume.Email = updateDto.Email;
        resume.PhoneNumber = updateDto.PhoneNumber;
        resume.Location = updateDto.Location;
        resume.ProfessionalSummary = updateDto.ProfessionalSummary;
        resume.Skills = updateDto.Skills;
        resume.Education = updateDto.Education;
        resume.Projects = updateDto.Projects;
        resume.Experience = updateDto.Experience;
        resume.Certifications = updateDto.Certifications;
        resume.GithubUrl = updateDto.GithubUrl;
        resume.LinkedInUrl = updateDto.LinkedinUrl;
        resume.PortfolioUrl = updateDto.PortfolioUrl;

        var updatedResume = await _resumeRepository.UpdateResumeAsync(resume);
        return MapToDto(updatedResume);
    }

    public async Task<bool> DeleteResumeAsync(int userId, int resumeId)
    {
        var resume = await _resumeRepository.GetResumeByIdAsync(resumeId);
        if (resume == null || resume.UserId != userId) return false;

        return await _resumeRepository.DeleteResumeAsync(resumeId);
    }

    public async Task<byte[]> GeneratePdfAsync(int userId)
    {
        var resume = await _resumeRepository.GetResumeByUserIdAsync(userId);
        if (resume == null) return Array.Empty<byte>();

        QuestPDF.Settings.License = LicenseType.Community;

        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(0.5f, Unit.Inch);
                page.PageColor(Colors.White);
                page.DefaultTextStyle(x => x.FontSize(10).FontFamily(Fonts.Verdana).FontColor(Colors.Grey.Darken4));

                page.Content().Column(col =>
                {
                    // --- HEADER (ONLY ON FIRST PAGE) ---
                    col.Item().Column(headerCol =>
                    {
                        headerCol.Item().AlignCenter().Text(resume.FullName.ToUpper()).FontSize(24).ExtraBold().LetterSpacing(0.05f).FontColor(Colors.Black);
                        
                        headerCol.Item().PaddingTop(2).AlignCenter().Row(row =>
                        {
                            row.Spacing(8);
                            if (!string.IsNullOrEmpty(resume.Email)) row.AutoItem().Text(resume.Email).FontSize(8);
                            if (!string.IsNullOrEmpty(resume.PhoneNumber)) 
                            {
                                row.AutoItem().Text("|").FontSize(8).FontColor(Colors.Grey.Lighten1);
                                row.AutoItem().Text(resume.PhoneNumber).FontSize(8);
                            }
                            if (!string.IsNullOrEmpty(resume.Location)) 
                            {
                                row.AutoItem().Text("|").FontSize(8).FontColor(Colors.Grey.Lighten1);
                                row.AutoItem().Text(resume.Location).FontSize(8);
                            }
                        });

                        headerCol.Item().PaddingTop(2).AlignCenter().Row(row =>
                        {
                            row.Spacing(8);
                            if (!string.IsNullOrEmpty(resume.LinkedInUrl)) 
                                row.AutoItem().Hyperlink(EnsureUrl(resume.LinkedInUrl)).Text("LinkedIn").FontSize(7).FontColor(Colors.Blue.Medium).Underline();
                            
                            if (!string.IsNullOrEmpty(resume.GithubUrl)) 
                                row.AutoItem().Hyperlink(EnsureUrl(resume.GithubUrl)).Text("GitHub").FontSize(7).FontColor(Colors.Blue.Medium).Underline();
                            
                            if (!string.IsNullOrEmpty(resume.PortfolioUrl)) 
                                row.AutoItem().Hyperlink(EnsureUrl(resume.PortfolioUrl)).Text("Portfolio").FontSize(7).FontColor(Colors.Blue.Medium).Underline();
                        });

                        headerCol.Item().PaddingTop(8).LineHorizontal(1.5f).LineColor(Colors.Black);
                    });

                    // --- CONTENT SECTIONS ---
                    col.Item().PaddingVertical(10).Column(contentCol =>
                    {
                        AddCompactSection(contentCol, "PROFESSIONAL SUMMARY", resume.ProfessionalSummary);
                        AddCompactSection(contentCol, "SKILLS", resume.Skills);
                        AddCompactSection(contentCol, "WORK EXPERIENCE", resume.Experience);
                        AddCompactSection(contentCol, "EDUCATION", resume.Education);
                        AddCompactSection(contentCol, "PROJECTS", resume.Projects);
                        AddCompactSection(contentCol, "CERTIFICATIONS", resume.Certifications);
                    });
                });
            });
        });

        return document.GeneratePdf();
    }

    private string EnsureUrl(string url)
    {
        if (string.IsNullOrEmpty(url)) return "#";
        if (url.StartsWith("http://") || url.StartsWith("https://")) return url;
        return "https://" + url;
    }

    private void AddCompactSection(ColumnDescriptor col, string title, string? content)
    {
        if (string.IsNullOrEmpty(content)) return;

        col.Item().PaddingTop(10).Column(sectionCol =>
        {
            sectionCol.Item().Row(row =>
            {
                row.RelativeItem().Text(title).FontSize(11).ExtraBold().LetterSpacing(0.05f).FontColor(Colors.Black);
            });
            sectionCol.Item().PaddingTop(1).LineHorizontal(0.5f).LineColor(Colors.Grey.Lighten2);
            sectionCol.Item().PaddingTop(4).Text(content).LineHeight(1.2f).FontSize(9.5f);
        });
    }

    private static ResumeDto MapToDto(Resume resume)
    {
        return new ResumeDto
        {
            Id = resume.Id,
            UserId = resume.UserId,
            CareerId = resume.CareerId,
            FullName = resume.FullName,
            Email = resume.Email,
            PhoneNumber = resume.PhoneNumber,
            Location = resume.Location,
            ProfessionalSummary = resume.ProfessionalSummary,
            Skills = resume.Skills,
            Education = resume.Education,
            Projects = resume.Projects,
            Experience = resume.Experience,
            Certifications = resume.Certifications,
            GithubUrl = resume.GithubUrl,
            LinkedinUrl = resume.LinkedInUrl,
            PortfolioUrl = resume.PortfolioUrl,
            CreatedAt = resume.CreatedAt,
            UpdatedAt = resume.UpdatedAt
        };
    }
}

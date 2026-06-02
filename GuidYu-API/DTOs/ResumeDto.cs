using System.ComponentModel.DataAnnotations;

namespace GuidYu_API.DTOs;

public class ResumeDto
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public int? CareerId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? PhoneNumber { get; set; }
    public string? Location { get; set; }
    public string? ProfessionalSummary { get; set; }
    public string? Skills { get; set; }
    public string? Education { get; set; }
    public string? Projects { get; set; }
    public string? Experience { get; set; }
    public string? Certifications { get; set; }
    public string? GithubUrl { get; set; }
    public string? LinkedinUrl { get; set; }
    public string? PortfolioUrl { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class CreateResumeDto
{
    public int? CareerId { get; set; }
    
    [Required]
    public string FullName { get; set; } = string.Empty;
    
    [Required]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;
    
    public string? PhoneNumber { get; set; }
    public string? Location { get; set; }
    public string? ProfessionalSummary { get; set; }
    public string? Skills { get; set; }
    public string? Education { get; set; }
    public string? Projects { get; set; }
    public string? Experience { get; set; }
    public string? Certifications { get; set; }
    public string? GithubUrl { get; set; }
    public string? LinkedinUrl { get; set; }
    public string? PortfolioUrl { get; set; }
}

public class UpdateResumeDto : CreateResumeDto
{
}

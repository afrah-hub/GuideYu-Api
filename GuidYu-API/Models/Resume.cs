using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace GuidYu_API.Models;

public class Resume
{
    [Key]
    public int Id { get; set; }

    [Required]
    public int UserId { get; set; }

    public int? CareerId { get; set; }

    [Required]
    [StringLength(100)]
    public string FullName { get; set; } = string.Empty;

    [Required]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;

    [StringLength(20)]
    public string? PhoneNumber { get; set; }

    [StringLength(200)]
    public string? Location { get; set; }

    public string? ProfessionalSummary { get; set; }

    public string? Skills { get; set; } // Can be stored as JSON or comma-separated

    public string? Education { get; set; } // JSON string

    public string? Projects { get; set; } // JSON string

    public string? Experience { get; set; } // JSON string

    public string? Certifications { get; set; } // JSON string

    public string? GithubUrl { get; set; }

    public string? LinkedInUrl { get; set; }

    public string? PortfolioUrl { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties (optional, if I want to use them)
    // [ForeignKey("UserId")]
    // public User? User { get; set; }
}

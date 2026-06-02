namespace GuidYu_API.Models;

public class User
{
    public int Id { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string? GoogleId { get; set; }
    public string? AppleId { get; set; }
    public string AuthProvider { get; set; } = "Manual";
    public DateTime CreatedAt { get; set; }
    public bool IsEmailVerified { get; set; }
    public DateTime? LastLoginAt { get; set; }

    public string? HighestQualification { get; set; }
    public string? Stream { get; set; }
    public string? InstitutionName { get; set; }
    public string? CurrentStatus { get; set; }

    public string? CareerGoal { get; set; }
    public string? PreferredIndustry { get; set; }

    public string? Skills { get; set; }
    public string? SkillLevels { get; set; }
    public string? Interests { get; set; }


    public string? RefreshToken { get; set; }
    public DateTime? RefreshTokenExpiryTime { get; set; }
    public string Role { get; set; } = "User";
    public string? ProfileImageUrl { get; set; }
    public bool IsBlocked { get; set; }
}

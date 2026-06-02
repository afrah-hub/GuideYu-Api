using System.Text.Json.Serialization;

namespace GuidYu_API.DTOs;

public class UpdateProfileRequest
{
    [JsonPropertyName("fullName")]
    public string? FullName { get; set; }
    public string? HighestQualification { get; set; }
    public string? Stream { get; set; }
    public string? InstitutionName { get; set; }
    public string? CurrentStatus { get; set; }

    public string? CareerGoal { get; set; }
    public string? PreferredIndustry { get; set; }

    public string? Skills { get; set; }
    public string? SkillLevels { get; set; }
    public string? Interests { get; set; }
}

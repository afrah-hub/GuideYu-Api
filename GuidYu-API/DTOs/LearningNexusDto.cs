using System.Collections.Generic;

namespace GuidYu_API.DTOs;

public class LearningNexusResponse
{
    public string TargetCareer { get; set; } = string.Empty;
    public int OverallProgress { get; set; }
    public List<SkillGapDto> Skills { get; set; } = new();
}

public class SkillGapDto
{
    public string Name { get; set; } = string.Empty;
    public string Priority { get; set; } = string.Empty;
    public string Status { get; set; } = "Not Started";
    public List<LearningResourceDto> Resources { get; set; } = new();
    public List<string> Projects { get; set; } = new();
}

public class LearningResourceDto
{
    public string Title { get; set; } = string.Empty;
    public string Platform { get; set; } = string.Empty;
    public string Difficulty { get; set; } = string.Empty;
}

namespace GuidYu_API.DTOs;

public class CareerPathSummaryDto
{
    public string CurrentRole { get; set; } = string.Empty;
    public string TargetRole { get; set; } = string.Empty;
    public int MatchPercentage { get; set; }
    public string EstimatedTime { get; set; } = string.Empty;
}

public class CareerPathStepDto
{
    public string RoleName { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty; // Completed, Current, Upcoming
    public bool IsCurrent { get; set; }
    public int Order { get; set; }
    public List<CareerPathSkillDto> Skills { get; set; } = new();
}

public class CareerPathSkillDto
{
    public string Name { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty; // Completed, InProgress, Missing
    public int Progress { get; set; }
    public string LearningTime { get; set; } = string.Empty;
    public List<LessonDto> Lessons { get; set; } = new();
}

public class LessonDto
{
    public string Title { get; set; } = string.Empty;
    public string Duration { get; set; } = string.Empty;
    public bool IsCompleted { get; set; }
}

public class CareerPathInsightDto
{
    public string Text { get; set; } = string.Empty;
    public string ImpactValue { get; set; } = string.Empty;
}

public class CareerPathOverviewDto
{
    public CareerPathSummaryDto Summary { get; set; } = new();
    public List<CareerPathStepDto> Journey { get; set; } = new();
    public List<CareerPathSkillDto> Skills { get; set; } = new();
    public List<CareerPathInsightDto> Insights { get; set; } = new();
}

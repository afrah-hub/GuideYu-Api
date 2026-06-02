using System.Collections.Generic;

namespace GuidYu_API.DTOs;

public class SyllabusResponse
{
    public string ModuleName { get; set; } = string.Empty;
    public string TargetCareer { get; set; } = string.Empty;
    public List<SyllabusTopicDto> Topics { get; set; } = new();
}

public class SyllabusTopicDto
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public List<string> KeyTakeaways { get; set; } = new();
    public string EstimatedTime { get; set; } = string.Empty;
    public string Difficulty { get; set; } = "Beginner"; // Beginner, Intermediate, Advanced
    public bool IsCompleted { get; set; }
    public int CompletedChaptersCount { get; set; }
    public int TotalChaptersCount { get; set; }
}

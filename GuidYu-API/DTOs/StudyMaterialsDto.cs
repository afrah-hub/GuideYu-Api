using System.Collections.Generic;

namespace GuidYu_API.DTOs;

public class StudyMaterialResponse
{
    public string TargetCareer { get; set; } = string.Empty;
    public List<StudyCategoryDto> Categories { get; set; } = new();
}

public class StudyCategoryDto
{
    public string CategoryName { get; set; } = string.Empty;
    public List<MaterialItemDto> Materials { get; set; } = new();
}

public class MaterialItemDto
{
    public string Title { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty; // Video, Article, E-book, Course
    public string Description { get; set; } = string.Empty;
    public string Link { get; set; } = "https://example.com";
    public string EstimatedTime { get; set; } = string.Empty;
}

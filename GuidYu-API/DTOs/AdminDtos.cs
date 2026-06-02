namespace GuidYu_API.DTOs;

public class AdminDashboardStatsDto
{
    public int TotalUsers { get; set; }
    public int ActiveUsers { get; set; }
    public int TotalCareers { get; set; }
    public int TotalRoadmaps { get; set; }
    public int LessonsGenerated { get; set; }
    public int AiRequestsCount { get; set; }
    public double UserCompletionRate { get; set; }
    public List<PopularCareerDto> PopularCareers { get; set; } = new();
}

public class PopularCareerDto
{
    public string Name { get; set; } = string.Empty;
    public int UserCount { get; set; }
}

public class CareerAdminDto
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Difficulty { get; set; }
    public string? Category { get; set; }
    public string? ThumbnailUrl { get; set; }
}

public class RoadmapAdminDto
{
    public int Id { get; set; }
    public int CareerId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
}

public class ModuleAdminDto
{
    public int Id { get; set; }
    public int RoadmapId { get; set; }
    public string Title { get; set; } = string.Empty;
    public int Order { get; set; }
    public List<TopicAdminDto> Topics { get; set; } = new();
}

public class TopicAdminDto
{
    public int Id { get; set; }
    public int ModuleId { get; set; }
    public string Title { get; set; } = string.Empty;
    public int Order { get; set; }
}

public class UserAdminDto
{
    public int Id { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime? LastLoginAt { get; set; }
    public bool IsBlocked { get; set; }
    public string? ProfileImageUrl { get; set; }
    public string? CareerGoal { get; set; }
    public string? Skills { get; set; }
    public string? Interests { get; set; }
    public double CompletionPercentage { get; set; }
}



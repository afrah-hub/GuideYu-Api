namespace GuidYu_API.DTOs;

public class DashboardOverviewDto
{
    public string Message { get; set; } = string.Empty;
    public double MarketPremium { get; set; }
    public int KeySkillsAway { get; set; }
    public string TargetRole { get; set; } = string.Empty;
    public int PathProgress { get; set; }
}

public class DashboardMetricDto
{
    public string Title { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public string Change { get; set; } = string.Empty;
    public string Icon { get; set; } = string.Empty;
    public string PreviousValue { get; set; } = string.Empty;
    public string TrendDirection { get; set; } = string.Empty; // up, down, neutral
    public string Timeframe { get; set; } = string.Empty;
    public string ActionAdvice { get; set; } = string.Empty;
    public int? ProgressPercentage { get; set; }
}

public class DashboardProgressDto
{
    public string Month { get; set; } = string.Empty;
    public int Velocity { get; set; }
}

public class DashboardNextStepDto
{
    public string Title { get; set; } = string.Empty;
    public int Score { get; set; }
    public List<string> MissingSkills { get; set; } = new();
    public string Icon { get; set; } = string.Empty;
    public bool IsPrimary { get; set; }
}

public class DashboardInsightDto
{
    public string Text { get; set; } = string.Empty;
    public string Highlight { get; set; } = string.Empty;
}

public class DashboardRoadmapDto
{
    public string Title { get; set; } = string.Empty;
    public int Progress { get; set; }
    public string TimeRemaining { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
}

public class DashboardActivityDto
{
    public string Type { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public string TimeAgo { get; set; } = string.Empty;
}

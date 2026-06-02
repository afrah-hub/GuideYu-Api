using System.Data;
using Dapper;
using GuidYu_API.DTOs;
using Microsoft.Data.SqlClient;
using System.Text.Json;

namespace GuidYu_API.Repositories;

public class DashboardRepository : IDashboardRepository
{
    private readonly string _connectionString;

    public DashboardRepository(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("DefaultConnection") 
            ?? throw new InvalidOperationException("DefaultConnection string not found.");
    }

    private IDbConnection CreateConnection() => new SqlConnection(_connectionString);

    private class OverviewUserDetails
    {
        public string? FullName { get; set; }
        public string? CareerGoal { get; set; }
        public string? Skills { get; set; }
        public string? CurrentStatus { get; set; }
    }

    public async Task<DashboardOverviewDto?> GetOverviewAsync(int userId)
    {
        using var connection = CreateConnection();
        const string sql = @"
            SELECT 
                u.FullName,
                ud.CareerGoal,
                ud.CurrentStatus,
                ud.Skills
            FROM Users u 
            LEFT JOIN UserDetails ud ON u.Id = ud.UserId
            WHERE u.Id = @UserId";
        
        var userDetails = await connection.QuerySingleOrDefaultAsync<OverviewUserDetails>(sql, new { UserId = userId });
        
        if (userDetails == null) return null;

        var skillsCount = string.IsNullOrWhiteSpace(userDetails.Skills) ? 0 : userDetails.Skills.Split(',').Length;
        var targetRole = !string.IsNullOrWhiteSpace(userDetails.CareerGoal) ? userDetails.CareerGoal : "Your Dream Role";
        
        var pathProgress = Math.Min(100, Math.Max(10, skillsCount * 12));
        var keySkillsAway = Math.Max(1, 5 - (skillsCount / 3));
        var marketPremium = 5.0 + (skillsCount * 0.4);

        return new DashboardOverviewDto
        {
            Message = userDetails.FullName ?? "User",
            MarketPremium = Math.Round(marketPremium, 1),
            KeySkillsAway = keySkillsAway,
            TargetRole = targetRole,
            PathProgress = pathProgress
        };
    }

    public async Task<IEnumerable<DashboardMetricDto>> GetMetricsAsync(int userId)
    {
        using var connection = CreateConnection();
        const string sql = "SELECT JsonData FROM DashboardData WHERE UserId = @UserId AND Category = 'Metric'";
        var jsonResults = await connection.QueryAsync<string>(sql, new { UserId = userId });

        if (jsonResults.Any())
        {
            return jsonResults.Select(json => JsonSerializer.Deserialize<DashboardMetricDto>(json)!).ToList();
        }

        // Fallback calculation if no metrics in DashboardData
        var userSql = @"
            SELECT ud.CurrentStatus, ud.CareerGoal, ud.Skills 
            FROM Users u
            LEFT JOIN UserDetails ud ON u.Id = ud.UserId
            WHERE u.Id = @UserId";
        var userDetails = await connection.QuerySingleOrDefaultAsync<OverviewUserDetails>(userSql, new { UserId = userId });
        
        var skillsCount = string.IsNullOrWhiteSpace(userDetails?.Skills) ? 0 : userDetails.Skills.Split(',').Length;
        var currentStatus = !string.IsNullOrWhiteSpace(userDetails?.CurrentStatus) ? userDetails.CurrentStatus : "Beginner";
        var careerGoal = !string.IsNullOrWhiteSpace(userDetails?.CareerGoal) ? userDetails.CareerGoal : "your target role";
        
        int currentScore = Math.Min(100, skillsCount * 12);
        int previousScore = Math.Max(0, currentScore - 12); 
        double percentageChange = previousScore == 0 && currentScore > 0 ? 100 : 
                                  previousScore == 0 ? 0 : 
                                  Math.Round((double)(currentScore - previousScore) / previousScore * 100, 1);
        string trend = percentageChange > 0 ? "up" : percentageChange < 0 ? "down" : "neutral";

        return new List<DashboardMetricDto>
        {
            new() { 
                Title = "Career Level", 
                Value = currentStatus, 
                PreviousValue = "Entry Level", 
                Change = "Updated", 
                TrendDirection = "up",
                Timeframe = "this week",
                ActionAdvice = "Complete more milestones to advance",
                ProgressPercentage = Math.Min(100, skillsCount * 25),
                Icon = "TrendingUp" 
            },
            new() { 
                Title = "Skill Index", 
                Value = currentScore.ToString(), 
                PreviousValue = previousScore.ToString(), 
                Change = $"+{percentageChange}%", 
                TrendDirection = trend,
                Timeframe = "last 30 days",
                ActionAdvice = "Add technical skills to boost your index",
                ProgressPercentage = currentScore,
                Icon = "Target" 
            },
            new() { 
                Title = "Path Completion", 
                Value = $"{currentScore}%", 
                PreviousValue = $"{previousScore}%", 
                Change = $"+{percentageChange}%", 
                TrendDirection = trend,
                Timeframe = "this week",
                ActionAdvice = $"Complete your next step for {careerGoal}",
                ProgressPercentage = currentScore,
                Icon = "Zap" 
            }
        };
    }

    public async Task<IEnumerable<DashboardProgressDto>> GetProgressAsync(int userId)
    {
        using var connection = CreateConnection();
        const string sql = "SELECT JsonData FROM DashboardData WHERE UserId = @UserId AND Category = 'Progress'";
        var results = await connection.QueryAsync<string>(sql, new { UserId = userId });
        return results.Select(json => JsonSerializer.Deserialize<DashboardProgressDto>(json)!);
    }

    private class CareerTargetRow
    {
        public string Title { get; set; } = string.Empty;
        public int Score { get; set; }
        public string MissingSkills { get; set; } = string.Empty;
        public string Icon { get; set; } = string.Empty;
        public bool IsPrimary { get; set; }
    }

    public async Task<IEnumerable<DashboardNextStepDto>> GetNextStepsAsync(int userId)
    {
        using var connection = CreateConnection();
        const string sql = "SELECT JsonData FROM DashboardData WHERE UserId = @UserId AND Category = 'NextStep'";
        var results = await connection.QueryAsync<string>(sql, new { UserId = userId });
        
        if (results.Any())
        {
            return results.Select(json => JsonSerializer.Deserialize<DashboardNextStepDto>(json)!);
        }

        var userSql = @"SELECT ud.CareerGoal FROM Users u LEFT JOIN UserDetails ud ON u.Id = ud.UserId WHERE u.Id = @UserId";
        var goal = await connection.ExecuteScalarAsync<string>(userSql, new { UserId = userId });
        var target = !string.IsNullOrWhiteSpace(goal) ? goal : "Advanced Role";
        
        return new List<DashboardNextStepDto>
        {
            new() { Title = target, Score = 85, MissingSkills = new List<string> { $"Advanced {target}", "Leadership" }, Icon = "Target", IsPrimary = true },
            new() { Title = $"Related {target}", Score = 65, MissingSkills = new List<string> { "Domain Knowledge" }, Icon = "Briefcase", IsPrimary = false }
        };
    }

    public async Task<IEnumerable<DashboardInsightDto>> GetInsightsAsync(int userId)
    {
        using var connection = CreateConnection();
        const string sql = "SELECT JsonData FROM DashboardData WHERE UserId = @UserId AND Category = 'Insight'";
        var results = await connection.QueryAsync<string>(sql, new { UserId = userId });
        
        if (results.Any())
        {
            return results.Select(json => JsonSerializer.Deserialize<DashboardInsightDto>(json)!);
        }

        var userSql = @"SELECT ud.CareerGoal FROM Users u LEFT JOIN UserDetails ud ON u.Id = ud.UserId WHERE u.Id = @UserId";
        var goal = await connection.ExecuteScalarAsync<string>(userSql, new { UserId = userId }) ?? "your target role";
        
        return new List<DashboardInsightDto>
        {
            new() { Text = $"Improving your core skills can increase your match score for {goal} to", Highlight = "90%" },
            new() { Text = $"You are well-positioned for junior roles in", Highlight = goal }
        };
    }

    public async Task<IEnumerable<DashboardRoadmapDto>> GetRoadmapAsync(int userId)
    {
        using var connection = CreateConnection();
        const string sql = "SELECT JsonData FROM DashboardData WHERE UserId = @UserId AND Category = 'Roadmap'";
        var results = await connection.QueryAsync<string>(sql, new { UserId = userId });
        
        if (results.Any())
        {
            return results.Select(json => JsonSerializer.Deserialize<DashboardRoadmapDto>(json)!);
        }

        var userSql = @"SELECT ud.CareerGoal FROM Users u LEFT JOIN UserDetails ud ON u.Id = ud.UserId WHERE u.Id = @UserId";
        var goal = await connection.ExecuteScalarAsync<string>(userSql, new { UserId = userId }) ?? "Advanced Role";
        
        return new List<DashboardRoadmapDto>
        {
            new() { Title = $"Foundations of {goal}", Progress = 100, TimeRemaining = "Completed", Status = "Completed" },
            new() { Title = $"Intermediate {goal} Concepts", Progress = 30, TimeRemaining = "5h remaining", Status = "In Progress" },
            new() { Title = $"Advanced {goal} Architecture", Progress = 0, TimeRemaining = "12h remaining", Status = "Next Up" }
        };
    }

    public async Task<IEnumerable<DashboardActivityDto>> GetActivityAsync(int userId)
    {
        using var connection = CreateConnection();
        const string sql = "SELECT JsonData FROM DashboardData WHERE UserId = @UserId AND Category = 'Activity'";
        var results = await connection.QueryAsync<string>(sql, new { UserId = userId });
        
        if (results.Any())
        {
            return results.Select(json => JsonSerializer.Deserialize<DashboardActivityDto>(json)!);
        }

        return new List<DashboardActivityDto>
        {
            new() { Type = "CheckCircle2", Title = "Profile Completed", Content = "You successfully set up your professional profile.", TimeAgo = "Just now" },
            new() { Type = "Target", Title = "Career Goal Set", Content = "You defined your target career path.", TimeAgo = "Just now" }
        };
    }
}

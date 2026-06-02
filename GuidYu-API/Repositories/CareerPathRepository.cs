using System.Text.Json;
using System.Data;
using Dapper;
using GuidYu_API.DTOs;
using Microsoft.Data.SqlClient;

namespace GuidYu_API.Repositories;

public class CareerPathRepository : ICareerPathRepository
{
    private readonly string _connectionString;

    public CareerPathRepository(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("DefaultConnection") 
            ?? throw new InvalidOperationException("DefaultConnection string not found.");
    }

    private IDbConnection CreateConnection() => new SqlConnection(_connectionString);

    private class CareerPathUserDetails
    {
        public string? CurrentRole { get; set; }
        public string? TargetRole { get; set; }
        public string? UserSkills { get; set; }
    }

    public async Task<bool> SaveCareerPathAsync(int userId, CareerPathOverviewDto data)
    {
        using var connection = CreateConnection();
        var jsonData = System.Text.Json.JsonSerializer.Serialize(data);

        // 1. Persist full career path JSON to DashboardData
        const string sql = @"
            MERGE INTO DashboardData AS target
            USING (SELECT @UserId AS UserId, 'CareerPathOverview' AS Category) AS source
            ON (target.UserId = source.UserId AND target.Category = source.Category)
            WHEN MATCHED THEN
                UPDATE SET JsonData = @JsonData
            WHEN NOT MATCHED THEN
                INSERT (UserId, Category, JsonData)
                VALUES (@UserId, 'CareerPathOverview', @JsonData);";

        var rowsAffected = await connection.ExecuteAsync(sql, new { UserId = userId, JsonData = jsonData });

        // 2. Also update UserDetails.CareerGoal so the dashboard overview reflects the chosen career
        var targetRole = data?.Summary?.TargetRole;
        if (!string.IsNullOrWhiteSpace(targetRole))
        {
            const string updateCareerGoalSql = @"
                MERGE INTO UserDetails AS target
                USING (SELECT @UserId AS UserId) AS source
                ON (target.UserId = source.UserId)
                WHEN MATCHED THEN
                    UPDATE SET CareerGoal = @CareerGoal
                WHEN NOT MATCHED THEN
                    INSERT (UserId, CareerGoal)
                    VALUES (@UserId, @CareerGoal);";

            await connection.ExecuteAsync(updateCareerGoalSql, new { UserId = userId, CareerGoal = targetRole });
        }

        return rowsAffected > 0;
    }

    public async Task<CareerPathOverviewDto> GetCareerPathOverviewAsync(int userId, string? targetCareer = null)
    {
        using var connection = CreateConnection();
        // Try to fetch stored AI-generated overview from DashboardData
        const string sqlOverview = @"SELECT JsonData FROM DashboardData WHERE UserId = @UserId AND Category = 'CareerPathOverview'";
        var json = await connection.QuerySingleOrDefaultAsync<string>(sqlOverview, new { UserId = userId });
        if (!string.IsNullOrWhiteSpace(json))
        {
            try
            {
                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                var overview = JsonSerializer.Deserialize<CareerPathOverviewDto>(json, options);
                if (overview != null)
                    return overview;
            }
            catch
            {
                // If deserialization fails, fall back to null to let service handle empty response
            }
        }
        // Optionally, you could still return user details as part of a minimal overview, but current flow expects null for empty.
        return null;
    }

    public async Task<CareerPathOverviewDto> GetSeededCareerPathOverviewAsync(int userId, string targetCareer)
    {
        var mappedCareer = targetCareer;
        var lowerCareer = targetCareer.ToLower();

        if (lowerCareer.Contains("front") || lowerCareer.Contains("back") || lowerCareer.Contains("full") || 
            lowerCareer.Contains("react") || lowerCareer.Contains(".net") || lowerCareer.Contains("web") || 
            lowerCareer.Contains("api") || lowerCareer.Contains("java") || lowerCareer.Contains("software"))
        {
            mappedCareer = "Full Stack Developer";
        }
        else if (lowerCareer.Contains("cloud") || lowerCareer.Contains("aws") || lowerCareer.Contains("azure") || 
                 lowerCareer.Contains("devops") || lowerCareer.Contains("platform") || lowerCareer.Contains("kubernetes") || 
                 lowerCareer.Contains("infrastructure") || lowerCareer.Contains("reliability"))
        {
            mappedCareer = "Cloud Solutions Architect";
        }
        else if (lowerCareer.Contains("sec") || lowerCareer.Contains("hack") || lowerCareer.Contains("pen") || 
                 lowerCareer.Contains("threat") || lowerCareer.Contains("cyber") || lowerCareer.Contains("forensic"))
        {
            mappedCareer = "Cybersecurity Analyst";
        }
        else if (lowerCareer.Contains("design") || lowerCareer.Contains("ui") || lowerCareer.Contains("ux") || 
                 lowerCareer.Contains("visual") || lowerCareer.Contains("graphic") || lowerCareer.Contains("motion"))
        {
            mappedCareer = "UI/UX Designer";
        }
        else if (lowerCareer.Contains("product") || lowerCareer.Contains("project") || lowerCareer.Contains("scrum") || 
                 lowerCareer.Contains("consult") || lowerCareer.Contains("analyst") || lowerCareer.Contains("strateg"))
        {
            mappedCareer = "Product Manager";
        }
        else if (lowerCareer.Contains("growth") || lowerCareer.Contains("operation") || lowerCareer.Contains("marketing"))
        {
            mappedCareer = "Growth Operations Lead";
        }

        using var connection = CreateConnection();
        const string careerSql = "SELECT Id, Title, Description, Difficulty, Category FROM Careers WHERE Title = @Title";
        var career = await connection.QueryFirstOrDefaultAsync<dynamic>(careerSql, new { Title = mappedCareer });

        if (career != null)
        {
            int careerId = (int)career.Id;
            const string roadmapSql = "SELECT Id, Title, Description FROM Roadmaps WHERE CareerId = @CareerId";
            var roadmap = await connection.QueryFirstOrDefaultAsync<dynamic>(roadmapSql, new { CareerId = careerId });
            
            if (roadmap != null)
            {
                int roadmapId = (int)roadmap.Id;
                const string modulesSql = "SELECT Id, Title, [Order] FROM RoadmapModules WHERE RoadmapId = @RoadmapId ORDER BY [Order]";
                var modules = await connection.QueryAsync<dynamic>(modulesSql, new { RoadmapId = roadmapId });
                
                var journey = new List<CareerPathStepDto>();
                var allSkills = new List<CareerPathSkillDto>();
                int moduleIndex = 0;
                
                foreach (var module in modules)
                {
                    int moduleId = (int)module.Id;
                    const string topicsSql = "SELECT Id, Title, [Order], Difficulty FROM ModuleTopics WHERE ModuleId = @ModuleId ORDER BY [Order]";
                    var topics = await connection.QueryAsync<dynamic>(topicsSql, new { ModuleId = moduleId });
                    
                    var skillsList = new List<CareerPathSkillDto>();
                    foreach (var topic in topics)
                    {
                        var skillDto = new CareerPathSkillDto
                        {
                            Name = topic.Title,
                            Category = moduleIndex == 0 ? "InProgress" : "Missing",
                            Progress = moduleIndex == 0 ? 25 : 0,
                            LearningTime = "2h 30m",
                            Lessons = new List<LessonDto>
                            {
                                new() { Title = $"Introduction to {topic.Title}", Duration = "15m", IsCompleted = false },
                                new() { Title = $"Practical Applications of {topic.Title}", Duration = "30m", IsCompleted = false }
                            }
                        };
                        skillsList.Add(skillDto);
                        allSkills.Add(skillDto);
                    }
                    
                    journey.Add(new CareerPathStepDto
                    {
                        RoleName = module.Title,
                        Status = moduleIndex == 0 ? "Current" : "Upcoming",
                        IsCurrent = moduleIndex == 0,
                        Order = module.Order,
                        Skills = skillsList
                    });
                    
                    moduleIndex++;
                }
                
                var overview = new CareerPathOverviewDto
                {
                    Summary = new CareerPathSummaryDto
                    {
                        CurrentRole = "Beginner",
                        TargetRole = targetCareer,
                        MatchPercentage = 85,
                        EstimatedTime = "6-12 months"
                    },
                    Journey = journey,
                    Skills = allSkills,
                    Insights = new List<CareerPathInsightDto>
                    {
                        new() { Text = $"Learning {(allSkills.FirstOrDefault()?.Name ?? "Foundations")} can increase your market value by 18%", ImpactValue = "18%" },
                        new() { Text = $"High industry demand for {targetCareer} specialists this quarter.", ImpactValue = "High" }
                    }
                };
                
                await SaveCareerPathAsync(userId, overview);
                return overview;
            }
        }

        // Deep fallback in 1ms if no DB entry exists
        var fallbackOverview = new CareerPathOverviewDto
        {
            Summary = new CareerPathSummaryDto
            {
                CurrentRole = "Beginner",
                TargetRole = targetCareer,
                MatchPercentage = 75,
                EstimatedTime = "1-2 years"
            },
            Journey = new List<CareerPathStepDto>
            {
                new() {
                    RoleName = "Foundational Stage",
                    Status = "Current",
                    IsCurrent = true,
                    Order = 1,
                    Skills = new List<CareerPathSkillDto> {
                        new() { Name = $"{targetCareer} Basics", Category = "InProgress", Progress = 25, LearningTime = "3h" }
                    }
                },
                new() {
                    RoleName = "Intermediate Implementation",
                    Status = "Upcoming",
                    IsCurrent = false,
                    Order = 2,
                    Skills = new List<CareerPathSkillDto> {
                        new() { Name = $"Advanced {targetCareer} Concepts", Category = "Missing", Progress = 0, LearningTime = "5h" }
                    }
                },
                new() {
                    RoleName = "Mastery & Integration",
                    Status = "Upcoming",
                    IsCurrent = false,
                    Order = 3,
                    Skills = new List<CareerPathSkillDto> {
                        new() { Name = $"{targetCareer} Capstone Project", Category = "Missing", Progress = 0, LearningTime = "8h" }
                    }
                }
            },
            Skills = new List<CareerPathSkillDto> {
                new() { Name = $"{targetCareer} Basics", Category = "InProgress", Progress = 25, LearningTime = "3h" },
                new() { Name = $"Advanced {targetCareer} Concepts", Category = "Missing", Progress = 0, LearningTime = "5h" },
                new() { Name = $"{targetCareer} Capstone Project", Category = "Missing", Progress = 0, LearningTime = "8h" }
            },
            Insights = new List<CareerPathInsightDto>
            {
                new() { Text = $"Developing specialized skills in {targetCareer} will accelerate your market growth.", ImpactValue = "Growth" },
                new() { Text = "Focus on practical portfolio building to stand out to hiring managers.", ImpactValue = "Portfolio" }
            }
        };

        await SaveCareerPathAsync(userId, fallbackOverview);
        return fallbackOverview;
    }
}

using System.Data;
using Dapper;
using GuidYu_API.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;

namespace GuidYu_API.Controllers;

[Authorize(Roles = "Admin")]
[ApiController]
[Route("api/admin/dashboard")]
public class AdminDashboardController : ControllerBase
{
    private readonly string _connectionString;

    public AdminDashboardController(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("DefaultConnection") 
            ?? throw new InvalidOperationException("DefaultConnection string not found.");
    }

    private IDbConnection CreateConnection() => new SqlConnection(_connectionString);

    [HttpGet("stats")]
    public async Task<IActionResult> GetStats()
    {
        using var connection = CreateConnection();
        
        var totalUsers = await connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM Users");
        var activeUsers = await connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM Users WHERE LastLoginAt > DATEADD(day, -30, GETDATE())");
        var totalCareers = await connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM Careers");
        var totalRoadmaps = await connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM Roadmaps");
        
        // Mocking some data for the ones we don't have tables for yet or that are complex
        var lessonsGenerated = 1250; // In a real app, count from LessonLogs table
        var aiRequestsCount = 4500;
        var userCompletionRate = 68.5;

        var popularCareersSql = @"
            SELECT TOP 5 Title as Name, COUNT(*) as UserCount 
            FROM Careers c
            JOIN UserDetails ud ON ud.CareerGoal = c.Title
            GROUP BY Title
            ORDER BY UserCount DESC";
        
        var popularCareers = (await connection.QueryAsync<PopularCareerDto>(popularCareersSql)).ToList();

        var stats = new AdminDashboardStatsDto
        {
            TotalUsers = totalUsers,
            ActiveUsers = activeUsers,
            TotalCareers = totalCareers,
            TotalRoadmaps = totalRoadmaps,
            LessonsGenerated = lessonsGenerated,
            AiRequestsCount = aiRequestsCount,
            UserCompletionRate = userCompletionRate,
            PopularCareers = popularCareers
        };

        return Ok(stats);
    }
}

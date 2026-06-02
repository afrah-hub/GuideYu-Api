using System.Data;
using Dapper;
using GuidYu_API.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;

namespace GuidYu_API.Controllers;

[Authorize]
[ApiController]
[Route("api/admin/careers")]
public class AdminCareersController : ControllerBase
{
    private readonly string _connectionString;

    public AdminCareersController(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("DefaultConnection") 
            ?? throw new InvalidOperationException("DefaultConnection string not found.");
    }

    private IDbConnection CreateConnection() => new SqlConnection(_connectionString);

    [HttpGet]
    public async Task<IActionResult> GetAllCareers()
    {
        using var connection = CreateConnection();
        var sql = "SELECT * FROM Careers ORDER BY CreatedAt DESC";
        var careers = await connection.QueryAsync<CareerAdminDto>(sql);
        return Ok(careers);
    }

    [HttpPost]
    public async Task<IActionResult> CreateCareer([FromBody] CareerAdminDto career)
    {
        using var connection = CreateConnection();
        var sql = @"
            INSERT INTO Careers (Title, Description, Difficulty, Category, ThumbnailUrl)
            VALUES (@Title, @Description, @Difficulty, @Category, @ThumbnailUrl);
            SELECT SCOPE_IDENTITY();";
        
        var id = await connection.ExecuteScalarAsync<int>(sql, career);
        career.Id = id;
        return Ok(career);
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateCareer(int id, [FromBody] CareerAdminDto career)
    {
        using var connection = CreateConnection();
        var sql = @"
            UPDATE Careers 
            SET Title = @Title, Description = @Description, Difficulty = @Difficulty, 
                Category = @Category, ThumbnailUrl = @ThumbnailUrl
            WHERE Id = @Id";
        
        career.Id = id;
        await connection.ExecuteAsync(sql, career);
        return Ok(career);
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteCareer(int id)
    {
        using var connection = CreateConnection();
        var sql = "DELETE FROM Careers WHERE Id = @Id";
        await connection.ExecuteAsync(sql, new { Id = id });
        return Ok();
    }

    // Roadmaps
    [HttpGet("{careerId}/roadmaps")]
    public async Task<IActionResult> GetRoadmaps(int careerId)
    {
        using var connection = CreateConnection();
        var sql = "SELECT * FROM Roadmaps WHERE CareerId = @CareerId";
        var roadmaps = await connection.QueryAsync<RoadmapAdminDto>(sql, new { CareerId = careerId });
        return Ok(roadmaps);
    }

    [HttpPost("roadmaps")]
    public async Task<IActionResult> CreateRoadmap([FromBody] RoadmapAdminDto roadmap)
    {
        using var connection = CreateConnection();
        var sql = @"
            INSERT INTO Roadmaps (CareerId, Title, Description)
            VALUES (@CareerId, @Title, @Description);
            SELECT SCOPE_IDENTITY();";
        
        var id = await connection.ExecuteScalarAsync<int>(sql, roadmap);
        roadmap.Id = id;
        return Ok(roadmap);
    }

    // Modules
    [HttpGet("roadmaps/{roadmapId}/modules")]
    public async Task<IActionResult> GetModules(int roadmapId)
    {
        using var connection = CreateConnection();
        var sqlModules = "SELECT * FROM RoadmapModules WHERE RoadmapId = @RoadmapId ORDER BY [Order]";
        var modules = (await connection.QueryAsync<ModuleAdminDto>(sqlModules, new { RoadmapId = roadmapId })).ToList();

        foreach (var module in modules)
        {
            var sqlTopics = "SELECT * FROM ModuleTopics WHERE ModuleId = @ModuleId ORDER BY [Order]";
            module.Topics = (await connection.QueryAsync<TopicAdminDto>(sqlTopics, new { ModuleId = module.Id })).ToList();
        }

        return Ok(modules);
    }

    [HttpPost("modules")]
    public async Task<IActionResult> CreateModule([FromBody] ModuleAdminDto module)
    {
        using var connection = CreateConnection();
        var sql = @"
            INSERT INTO RoadmapModules (RoadmapId, Title, [Order])
            VALUES (@RoadmapId, @Title, @Order);
            SELECT SCOPE_IDENTITY();";
        
        var id = await connection.ExecuteScalarAsync<int>(sql, module);
        module.Id = id;
        return Ok(module);
    }

    [HttpPost("topics")]
    public async Task<IActionResult> CreateTopic([FromBody] TopicAdminDto topic)
    {
        using var connection = CreateConnection();
        var sql = @"
            INSERT INTO ModuleTopics (ModuleId, Title, [Order])
            VALUES (@ModuleId, @Title, @Order);
            SELECT SCOPE_IDENTITY();";
        
        var id = await connection.ExecuteScalarAsync<int>(sql, topic);
        topic.Id = id;
        return Ok(topic);
    }
}

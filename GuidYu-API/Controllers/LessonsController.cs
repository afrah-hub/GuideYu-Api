using GuidYu_API.DTOs;
using GuidYu_API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Dapper;
using System.Security.Claims;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace GuidYu_API.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class LessonsController : ControllerBase
{
    private readonly IAiService _aiService;
    private readonly IConfiguration _configuration;
    private readonly string _connectionString;

    private readonly ICareerPathService _careerPathService;
    public LessonsController(IAiService aiService, IConfiguration configuration, ICareerPathService careerPathService)
    {
        _aiService = aiService;
        _configuration = configuration;
        _connectionString = _configuration.GetConnectionString("DefaultConnection")!;
        _careerPathService = careerPathService;
    }

    [HttpGet("roadmap/{roadmapId}")]
    public async Task<IActionResult> GetRoadmapStructure(int roadmapId, [FromQuery] string? career = null, [FromQuery] string? stage = "Beginner")
    {
        using var connection = new SqlConnection(_connectionString);
        var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value!);

        // Determine target career
        if (string.IsNullOrWhiteSpace(career))
        {
            var overview = await _careerPathService.GetCareerPathOverviewAsync(userId, null, stage);
            career = overview?.Summary?.TargetRole ?? "Software Developer";
        }

        // 1. Check if a roadmap already exists in DB for this career name
        var careerEntity = await connection.QueryFirstOrDefaultAsync<dynamic>(
            "SELECT Id FROM Careers WHERE Title = @Title", new { Title = career });

        int dbRoadmapId = 0;

        if (careerEntity != null)
        {
            var dbRoadmap = await connection.QueryFirstOrDefaultAsync<dynamic>(
                "SELECT Id FROM Roadmaps WHERE CareerId = @CareerId", new { CareerId = (int)careerEntity.Id });

            if (dbRoadmap != null)
            {
                dbRoadmapId = (int)dbRoadmap.Id;
            }
        }

        // 2. Generate and save AI-driven roadmap if it doesn't exist
        if (dbRoadmapId == 0)
        {
            try
            {
                var careerOverview = await _careerPathService.GetCareerPathOverviewAsync(userId, career, stage);

                if (careerOverview != null && careerOverview.Journey != null && careerOverview.Journey.Any())
                {
                    // Insert the new Career
                    var newCareerId = await connection.QuerySingleAsync<int>(@"
                        INSERT INTO Careers (Title, Description, Difficulty, Category, CreatedAt)
                        VALUES (@Title, @Description, @Difficulty, @Category, GETDATE());
                        SELECT CAST(SCOPE_IDENTITY() as int);",
                        new {
                            Title = career,
                            Description = $"AI-generated personalized path for {career}.",
                            Difficulty = stage ?? "Beginner",
                            Category = "AI"
                        });

                    // Insert the Roadmap
                    dbRoadmapId = await connection.QuerySingleAsync<int>(@"
                        INSERT INTO Roadmaps (CareerId, Title, Description, CreatedAt)
                        VALUES (@CareerId, @Title, @Description, GETDATE());
                        SELECT CAST(SCOPE_IDENTITY() as int);",
                        new {
                            CareerId = newCareerId,
                            Title = $"{career} Mastery Path",
                            Description = $"The ultimate complete AI-generated roadmap to master {career} roles."
                        });

                    // Insert Modules and Topics
                    int modOrder = 1;
                    foreach (var step in careerOverview.Journey)
                    {
                        var newModuleId = await connection.QuerySingleAsync<int>(@"
                            INSERT INTO RoadmapModules (RoadmapId, Title, [Order])
                            VALUES (@RoadmapId, @Title, @Order);
                            SELECT CAST(SCOPE_IDENTITY() as int);",
                            new {
                                RoadmapId = dbRoadmapId,
                                Title = step.RoleName,
                                Order = modOrder++
                            });

                        // Insert topics (lessons)
                        int topicOrder = 1;
                        if (step.Skills != null)
                        {
                            var lessons = step.Skills.SelectMany(s => s.Lessons ?? new List<LessonDto>());
                            foreach (var lesson in lessons)
                            {
                                await connection.ExecuteAsync(@"
                                    INSERT INTO ModuleTopics (ModuleId, Title, Difficulty, [Order])
                                    VALUES (@ModuleId, @Title, @Difficulty, @Order)",
                                    new {
                                        ModuleId = newModuleId,
                                        Title = lesson.Title,
                                        Difficulty = stage ?? "Beginner",
                                        Order = topicOrder++
                                    });
                            }
                        }
                    }

                    roadmapId = dbRoadmapId;
                }
            }
            catch (Exception ex)
            {
                // If AI service returned an empty response, return an empty roadmap to avoid breaking the UI
                if (ex.Message?.Contains("AI Service returned an empty response") == true)
                {
                    // Return empty journey list
                    return Ok(new List<object>());
                }
                return StatusCode(500, new { message = "Failed to generate AI curriculum", details = ex.Message });
            }
        }
        else
        {
            roadmapId = dbRoadmapId;
        }

        // 3. Transform DB result into frontend-friendly journey DTO
        var modulesSql = @"
            SELECT m.Id, m.Title, m.[Order]
            FROM RoadmapModules m
            WHERE m.RoadmapId = @RoadmapId
            ORDER BY m.[Order]";

        var dbModules = await connection.QueryAsync<dynamic>(modulesSql, new { RoadmapId = roadmapId });

        var journey = new List<object>();
        bool foundCurrent = false;

        foreach (var module in dbModules)
        {
            var topicsSql = @"
                SELECT t.Id, t.Title, t.[Order], t.Difficulty,
                       CASE WHEN EXISTS (
                           SELECT 1 FROM LessonQuizResults lqr 
                           WHERE lqr.UserId = @UserId 
                             AND lqr.Passed = 1 
                             AND (lqr.LessonId = t.Title OR LOWER(REPLACE(REPLACE(lqr.LessonId, ' ', '-'), '/', '-')) = LOWER(REPLACE(REPLACE(t.Title, ' ', '-'), '/', '-')))
                       ) THEN 1
                       ELSE ISNULL(tp.IsCompleted, 0) END as IsCompleted
                FROM ModuleTopics t
                LEFT JOIN TopicProgress tp ON t.Id = tp.TopicId AND tp.UserId = @UserId
                WHERE t.ModuleId = @ModuleId
                ORDER BY t.[Order]";

            var topics = await connection.QueryAsync<dynamic>(topicsSql, new { ModuleId = module.Id, UserId = userId });
            var topicsWithKeys = topics.Select(t => new {
                t.Id,
                t.Title,
                t.Order,
                t.Difficulty,
                t.IsCompleted,
                TopicKey = ((string)t.Title).ToLower().Replace(" ", "-").Replace("/", "-")
            }).ToList();

            // Determine module status based on topics completion
            bool allCompleted = topicsWithKeys.Any() && topicsWithKeys.All(t => {
                object comp = t.IsCompleted;
                if (comp is bool b) return b;
                if (comp is int i) return i == 1;
                if (comp is long l) return l == 1;
                return false;
            });
            string status = allCompleted ? "Completed" : "InProgress";
            bool isCurrent = false;
            if (!allCompleted && !foundCurrent)
            {
                isCurrent = true;
                foundCurrent = true;
            }

            // Build journey items matching frontend expectations
                journey.Add(new {
                    Id = module.Id,
                    Title = module.Title,
                    Order = module.Order,
                    // Frontend expects a "Topics" array; include all topic fields
                    Topics = topicsWithKeys.Select(t => new {
                        Id = t.Id,
                        Title = t.Title,
                        Order = t.Order,
                        Difficulty = t.Difficulty,
                        IsCompleted = t.IsCompleted,
                        TopicKey = t.TopicKey
                    }).ToList()
                });
        }

        return Ok(journey);
    }

    [HttpGet("content")]
    public async Task<IActionResult> GetLessonContent(
        [FromQuery] string topic, 
        [FromQuery] string career, 
        [FromQuery] string difficulty = "Intermediate",
        [FromQuery] string? module = null)
    {
        try
        {
            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!string.IsNullOrEmpty(userIdStr) && int.TryParse(userIdStr, out var userId))
            {
                var modName = module ?? "General Module";
                using var connection = new SqlConnection(_connectionString);

                // Server-side Lesson Unlock Enforcement
                var moduleRecord = await connection.QueryFirstOrDefaultAsync<dynamic>(
                    @"SELECT rm.Id FROM RoadmapModules rm
                      JOIN Roadmaps r ON rm.RoadmapId = r.Id
                      JOIN Careers c ON r.CareerId = c.Id
                      WHERE rm.Title = @ModuleName AND c.Title = @Career",
                    new { ModuleName = modName, Career = career });

                if (moduleRecord != null)
                {
                    int moduleId = (int)moduleRecord.Id;
                    var topicRecord = await connection.QueryFirstOrDefaultAsync<dynamic>(
                        @"SELECT Id, [Order], Title FROM ModuleTopics 
                          WHERE ModuleId = @ModuleId 
                            AND (Title = @Topic OR LOWER(REPLACE(REPLACE(Title, ' ', '-'), '/', '-')) = LOWER(@Topic))",
                        new { ModuleId = moduleId, Topic = topic });

                    if (topicRecord != null)
                    {
                        int currentOrder = (int)topicRecord.Order;
                        if (currentOrder > 1)
                        {
                            // Find the previous topic (Order - 1) in the same module
                            var prevTopic = await connection.QueryFirstOrDefaultAsync<dynamic>(
                                @"SELECT Id, Title FROM ModuleTopics 
                                  WHERE ModuleId = @ModuleId AND [Order] = @PrevOrder",
                                new { ModuleId = moduleId, PrevOrder = currentOrder - 1 });

                            if (prevTopic != null)
                            {
                                // Check if previous topic is completed in TopicProgress
                                var isCompleted = await connection.QueryFirstOrDefaultAsync<bool>(
                                    @"SELECT ISNULL(IsCompleted, 0) FROM TopicProgress 
                                      WHERE UserId = @UserId AND TopicId = @TopicId",
                                    new { UserId = userId, TopicId = (int)prevTopic.Id });

                                if (!isCompleted)
                                {
                                    return StatusCode(403, new { message = "Complete the previous lesson quiz to unlock this lesson." });
                                }
                            }
                        }
                    }
                }

                // Step 1: Check database (SavedLessons table) first
                const string selectSavedSql = @"
                    SELECT GeneratedContent 
                    FROM SavedLessons 
                    WHERE UserId = @UserId 
                      AND CareerName = @CareerName 
                      AND ModuleName = @ModuleName 
                      AND LessonId = @LessonId";

                var existingContentJson = await connection.QueryFirstOrDefaultAsync<string>(selectSavedSql, new {
                    UserId = userId,
                    CareerName = career,
                    ModuleName = modName,
                    LessonId = topic
                });

                if (!string.IsNullOrEmpty(existingContentJson))
                {
                    try
                    {
                        var cachedLesson = JsonSerializer.Deserialize<LessonContentDto>(existingContentJson, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                        if (cachedLesson != null)
                        {
                            CleanAndNormalizeLesson(cachedLesson);
                            return Ok(cachedLesson);
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error deserializing cached lesson from SavedLessons: {ex.Message}");
                    }
                }

                // Step 2: Fallback to checking the legacy MyCareerMap LessonsData JSON dictionary
                var savedMap = await connection.QueryFirstOrDefaultAsync<dynamic>(
                    "SELECT Id, LessonsData FROM MyCareerMap WHERE UserId = @UserId AND CareerName = @CareerName AND ModuleName = @ModuleName",
                    new { UserId = userId, CareerName = career, ModuleName = modName }
                );

                var lessonsDict = new Dictionary<string, LessonContentDto>(StringComparer.OrdinalIgnoreCase);
                if (savedMap != null)
                {
                    string? lessonsJson = savedMap.LessonsData;
                    if (!string.IsNullOrEmpty(lessonsJson))
                    {
                        try
                        {
                            lessonsDict = JsonSerializer.Deserialize<Dictionary<string, LessonContentDto>>(lessonsJson)
                                          ?? new Dictionary<string, LessonContentDto>(StringComparer.OrdinalIgnoreCase);
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Error deserializing saved lessons from MyCareerMap: {ex.Message}");
                        }
                    }

                    if (lessonsDict.TryGetValue(topic, out var legacyLesson))
                    {
                        CleanAndNormalizeLesson(legacyLesson);
                        // Save it to the new SavedLessons table so it's cached there for next time
                        try
                        {
                            const string insertSavedSql = @"
                                IF NOT EXISTS (SELECT 1 FROM SavedLessons WHERE UserId = @UserId AND CareerName = @CareerName AND ModuleName = @ModuleName AND LessonId = @LessonId)
                                BEGIN
                                    INSERT INTO SavedLessons (UserId, CareerName, ModuleName, ChapterName, LessonId, GeneratedContent, CreatedAt)
                                    VALUES (@UserId, @CareerName, @ModuleName, @ChapterName, @LessonId, @GeneratedContent, GETDATE())
                                END";

                            await connection.ExecuteAsync(insertSavedSql, new {
                                UserId = userId,
                                CareerName = career,
                                ModuleName = modName,
                                ChapterName = legacyLesson.Title ?? topic,
                                LessonId = topic,
                                GeneratedContent = JsonSerializer.Serialize(legacyLesson)
                            });
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Error back-filling SavedLessons from legacy cache: {ex.Message}");
                        }

                        return Ok(legacyLesson);
                    }
                }

                // Step 3: If lesson content does NOT exist anywhere, call AI, generate lesson, save to database, return it
                var aiResponse = await _aiService.GenerateLessonContentAsync(topic, career, difficulty);
                var lessonContent = JsonSerializer.Deserialize<LessonContentDto>(aiResponse, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                if (lessonContent != null)
                {
                    CleanAndNormalizeLesson(lessonContent);
                    // Save to SavedLessons table
                    try
                    {
                        const string insertSavedSql = @"
                            INSERT INTO SavedLessons (UserId, CareerName, ModuleName, ChapterName, LessonId, GeneratedContent, CreatedAt)
                            VALUES (@UserId, @CareerName, @ModuleName, @ChapterName, @LessonId, @GeneratedContent, GETDATE())";

                        await connection.ExecuteAsync(insertSavedSql, new {
                            UserId = userId,
                            CareerName = career,
                            ModuleName = modName,
                            ChapterName = lessonContent.Title ?? topic,
                            LessonId = topic,
                            GeneratedContent = JsonSerializer.Serialize(lessonContent)
                        });
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error saving to SavedLessons: {ex.Message}");
                    }

                    // Save/Update to MyCareerMap.LessonsData for legacy fallback
                    if (savedMap != null)
                    {
                        lessonsDict[topic] = lessonContent;
                        var updatedLessonsJson = JsonSerializer.Serialize(lessonsDict);

                        await connection.ExecuteAsync(
                            "UPDATE MyCareerMap SET LessonsData = @LessonsData WHERE Id = @Id",
                            new { LessonsData = updatedLessonsJson, Id = (int)savedMap.Id }
                        );
                    }
                    else
                    {
                        lessonsDict[topic] = lessonContent;
                        var updatedLessonsJson = JsonSerializer.Serialize(lessonsDict);

                        var defaultSyllabus = new {
                            moduleName = modName,
                            targetCareer = career,
                            topics = new[] {
                                new { id = 1, title = topic, description = "AI-generated topic study.", difficulty = difficulty, estimatedTime = "30" }
                            }
                        };
                        var syllabusJson = JsonSerializer.Serialize(defaultSyllabus);

                        await connection.ExecuteAsync(
                            @"INSERT INTO MyCareerMap (UserId, CareerName, ModuleName, SyllabusData, LessonsData, CreatedAt) 
                              VALUES (@UserId, @CareerName, @ModuleName, @SyllabusData, @LessonsData, GETDATE())",
                            new { UserId = userId, CareerName = career, ModuleName = modName, SyllabusData = syllabusJson, LessonsData = updatedLessonsJson }
                        );
                    }

                    return Ok(lessonContent);
                }
            }

            // Normal dynamic / fallback flow (in case of missing query parameters or guest session)
            var jsonResponse = await _aiService.GenerateLessonContentAsync(topic, career, difficulty);
            var fallbackLessonContent = JsonSerializer.Deserialize<LessonContentDto>(jsonResponse, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            if (fallbackLessonContent != null)
            {
                CleanAndNormalizeLesson(fallbackLessonContent);
            }
            return Ok(fallbackLessonContent);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "Failed to generate lesson content", details = ex.Message });
        }
    }

    [HttpPost("ai/action")]
    public async Task<IActionResult> ProcessAiAction([FromBody] AiActionRequestDto request)
    {
        try
        {
            var response = await _aiService.ProcessAiActionAsync(request.ActionType, request.Context, request.TargetCareer);
            
            if (request.ActionType.ToLower() == "quiz")
            {
                try 
                {
                    var quizData = JsonSerializer.Deserialize<object>(response);
                    return Ok(new AiActionResponseDto { Response = "Quiz generated successfully", Metadata = quizData });
                }
                catch 
                {
                    return Ok(new AiActionResponseDto { Response = response });
                }
            }

            return Ok(new AiActionResponseDto { Response = response });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "AI Action failed", details = ex.Message });
        }
    }

    [HttpPost("progress")]
    public async Task<IActionResult> UpdateProgress([FromBody] LessonProgressUpdateDto request)
    {
        using var connection = new SqlConnection(_connectionString);
        var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value!);

        // Find topic Id by title and roadmap/module context (simplified for now by using title)
        var topicId = await connection.QueryFirstOrDefaultAsync<int?>(
            "SELECT Id FROM ModuleTopics WHERE Title = @Title", new { Title = request.TopicTitle });

        if (topicId == null) return NotFound(new { message = "Topic not found" });

        var sql = @"
            IF EXISTS (SELECT 1 FROM TopicProgress WHERE UserId = @UserId AND TopicId = @TopicId)
            BEGIN
                UPDATE TopicProgress 
                SET IsCompleted = CASE WHEN @IsCompleted = 1 THEN 1 ELSE IsCompleted END, 
                    LastAccessed = GETDATE()
                WHERE UserId = @UserId AND TopicId = @TopicId
            END
            ELSE
            BEGIN
                INSERT INTO TopicProgress (UserId, TopicId, IsCompleted)
                VALUES (@UserId, @TopicId, @IsCompleted)
            END";

        await connection.ExecuteAsync(sql, new { UserId = userId, TopicId = topicId, IsCompleted = request.IsCompleted });

        return Ok(new { message = "Progress updated" });
    }

    [HttpGet("last-stopped")]
    public async Task<IActionResult> GetLastStopped()
    {
        using var connection = new SqlConnection(_connectionString);
        var userIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdStr) || !int.TryParse(userIdStr, out var userId))
        {
            return Unauthorized();
        }

        const string sql = "SELECT JsonData FROM DashboardData WHERE UserId = @UserId AND Category = 'LastStoppedLesson'";
        var json = await connection.ExecuteScalarAsync<string>(sql, new { UserId = userId });

        if (string.IsNullOrEmpty(json))
        {
            return NoContent();
        }

        try
        {
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var data = JsonSerializer.Deserialize<LastStoppedLessonDto>(json, options);
            return Ok(data);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "Failed to deserialize last stopped lesson", details = ex.Message });
        }
    }

    [HttpPost("last-stopped")]
    public async Task<IActionResult> SaveLastStopped([FromBody] LastStoppedLessonDto request)
    {
        if (request == null) return BadRequest("Invalid request body");

        using var connection = new SqlConnection(_connectionString);
        var userIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdStr) || !int.TryParse(userIdStr, out var userId))
        {
            return Unauthorized();
        }

        var json = JsonSerializer.Serialize(request);

        const string sql = @"
            MERGE INTO DashboardData AS target
            USING (SELECT @UserId AS UserId, 'LastStoppedLesson' AS Category) AS source
            ON (target.UserId = source.UserId AND target.Category = source.Category)
            WHEN MATCHED THEN
                UPDATE SET JsonData = @JsonData, CreatedAt = GETDATE()
            WHEN NOT MATCHED THEN
                INSERT (UserId, Category, JsonData, CreatedAt)
                VALUES (@UserId, 'LastStoppedLesson', @JsonData, GETDATE());";

        var rowsAffected = await connection.ExecuteAsync(sql, new { UserId = userId, JsonData = json });
        
        if (rowsAffected > 0)
        {
            return Ok(new { message = "Last stopped lesson saved successfully." });
        }
        return StatusCode(500, "Failed to save last stopped lesson.");
    }

    private void CleanAndNormalizeLesson(LessonContentDto lesson)
    {
        if (lesson == null) return;
        lesson.Content = CleanAndNormalizeMarkdown(lesson.Content);
        lesson.Summary = CleanAndNormalizeMarkdown(lesson.Summary);
        lesson.Title = CleanAndNormalizeMarkdown(lesson.Title);
        
        if (lesson.KeyConcepts != null)
        {
            for (int i = 0; i < lesson.KeyConcepts.Count; i++)
            {
                lesson.KeyConcepts[i] = CleanAndNormalizeMarkdown(lesson.KeyConcepts[i]);
            }
        }
        
        if (lesson.Examples != null)
        {
            for (int i = 0; i < lesson.Examples.Count; i++)
            {
                lesson.Examples[i] = CleanAndNormalizeMarkdown(lesson.Examples[i]);
            }
        }
    }

    private string CleanAndNormalizeMarkdown(string text)
    {
        if (string.IsNullOrEmpty(text)) return text;
        
        // 1. Replace literal "\\n" sequence with actual newlines
        text = text.Replace("\\n", "\n");
        text = text.Replace("\\r", "\r");
        text = text.Replace("\\t", "\t");
        
        // 2. Normalize line breaks to \n
        text = text.Replace("\r\n", "\n").Replace("\r", "\n");
        
        return text.Trim();
    }
}


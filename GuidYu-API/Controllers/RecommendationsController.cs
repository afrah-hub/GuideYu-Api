using System.Security.Claims;
using System.Text.Json;
using GuidYu_API.Repositories;
using GuidYu_API.Services;
using GuidYu_API.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using System.Collections.Generic;
using Dapper;

namespace GuidYu_API.Controllers
{
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class RecommendationsController : ControllerBase
{
    private readonly IAiService _aiService;
    private readonly IUserRepository _userRepository;
    private readonly string _connectionString;

    public RecommendationsController(IAiService aiService, IUserRepository userRepository, IConfiguration configuration)
    {
        _aiService = aiService;
        _userRepository = userRepository;
        _connectionString = configuration.GetConnectionString("DefaultConnection")!;
    }

    [HttpGet("ai-careers")]
    public async Task<IActionResult> GetAICareers()
    {
        try
        {
            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userIdStr) || !int.TryParse(userIdStr, out var userId))
            {
                return Unauthorized(new { message = "Invalid token claims." });
            }

            var user = await _userRepository.GetUserByIdAsync(userId);
            if (user == null)
            {
                return NotFound(new { message = "User not found." });
            }

            // Extract user data for the prompt
            var skills = user.Skills ?? "Not provided";
            var interests = user.Interests ?? "Not provided";
            var qualification = user.HighestQualification ?? "Degree";
            var stream = user.Stream ?? "General";
            var education = $"{qualification} in {stream}";
            var careerTarget = user.CareerGoal ?? "Software Developer"; // Default if not provided

            var aiResponse = await _aiService.GetCareerRecommendationsAsync(skills, interests, education, careerTarget);

            if (string.IsNullOrWhiteSpace(aiResponse))
            {
                return StatusCode(500, new { message = "AI Service returned an empty response." });
            }

            try
            {
                var recommendations = JsonSerializer.Deserialize<AIRecommendationResponse>(aiResponse, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                return Ok(recommendations);
            }
            catch (JsonException ex)
            {
                return StatusCode(500, new { message = "AI returned an invalid JSON structure.", details = ex.Message, raw = aiResponse });
            }

        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "Critical error in recommendations controller.", details = ex.Message });
        }
    }

    [HttpGet("learning-plan")]
    public async Task<IActionResult> GetLearningPlan([FromQuery] string targetCareer)
    {
        try
        {
            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userIdStr) || !int.TryParse(userIdStr, out var userId))
            {
                return Unauthorized(new { message = "Invalid token claims." });
            }

            var user = await _userRepository.GetUserByIdAsync(userId);
            if (user == null)
            {
                return NotFound(new { message = "User not found." });
            }

            if (string.IsNullOrEmpty(targetCareer))
            {
                targetCareer = user.Interests?.Split(',').FirstOrDefault() ?? "Software Developer";
            }

            var skills = user.Skills ?? "Not provided";
            var aiResponse = await _aiService.GetLearningPlanAsync(targetCareer, skills);

            if (string.IsNullOrWhiteSpace(aiResponse))
            {
                return StatusCode(500, new { message = "AI Service returned an empty response." });
            }

            try
            {
                var learningPlan = JsonSerializer.Deserialize<LearningNexusResponse>(aiResponse, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                return Ok(learningPlan);
            }
            catch (JsonException ex)
            {
                return StatusCode(500, new { message = "AI returned invalid JSON structure.", details = ex.Message, raw = aiResponse });
            }
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "An error occurred while generating your learning plan.", details = ex.Message, inner = ex.InnerException?.Message });
        }
    }

    [HttpGet("study-materials")]
    public async Task<IActionResult> GetStudyMaterials()
    {
        try
        {
            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userIdStr) || !int.TryParse(userIdStr, out var userId))
            {
                return Unauthorized(new { message = "Invalid token claims." });
            }

            var user = await _userRepository.GetUserByIdAsync(userId);
            if (user == null)
            {
                return NotFound(new { message = "User not found." });
            }

            var targetCareer = user.Interests?.Split(',').FirstOrDefault() ?? "Software Developer";
            var skills = user.Skills ?? "Not provided";
            var education = $"{user.HighestQualification} in {user.Stream}" ?? "Not provided";

        try
        {
            var aiResponse = await _aiService.GetStudyMaterialsAsync(targetCareer, skills, education);

            if (string.IsNullOrWhiteSpace(aiResponse))
            {
                return StatusCode(500, new { message = "Empty response from AI service." });
            }

            try
            {
                var studyMaterials = JsonSerializer.Deserialize<GuidYu_API.DTOs.StudyMaterialResponse>(aiResponse, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
                return Ok(studyMaterials);
            }
            catch (JsonException ex)
            {
                return StatusCode(500, new { message = "AI returned invalid JSON structure.", details = ex.Message, raw = aiResponse });
            }
        }
        catch (Exception ex) when (ex.Message.Contains("429") || ex.Message.Contains("TooManyRequests"))
        {
            // Quota exceeded – return a graceful fallback response
            var fallback = new GuidYu_API.DTOs.StudyMaterialResponse
            {
                Categories = new List<GuidYu_API.DTOs.StudyCategoryDto>()
            };
            return StatusCode(429, new { message = "AI quota exceeded. Returning limited fallback data.", data = fallback });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "An error occurred while generating your study materials.", details = ex.Message });
        }
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "An error occurred while generating your study materials.", details = ex.Message });
        }
    }

    [HttpGet("syllabus")]
    public async Task<IActionResult> GetSyllabus([FromQuery] string moduleName, [FromQuery] string? targetCareer, [FromQuery] int? savedMapId)
    {
        try
        {
            if (string.IsNullOrEmpty(moduleName))
            {
                return BadRequest(new { message = "Module name is required." });
            }

            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            int.TryParse(userIdStr, out var userId);

            if (string.IsNullOrEmpty(targetCareer))
            {
                if (userId > 0)
                {
                    var user = await _userRepository.GetUserByIdAsync(userId);
                    targetCareer = user?.Interests?.Split(',').FirstOrDefault() ?? "Software Developer";
                }
                else
                {
                    targetCareer = "Software Developer";
                }
            }

            if (userId > 0)
            {
                using var connection = new SqlConnection(_connectionString);
                dynamic? savedMap = null;

                if (savedMapId.HasValue && savedMapId.Value > 0)
                {
                    savedMap = await connection.QueryFirstOrDefaultAsync<dynamic>(
                        "SELECT SyllabusData FROM MyCareerMap WHERE Id = @Id AND UserId = @UserId",
                        new { Id = savedMapId.Value, UserId = userId }
                    );
                }

                if (savedMap == null)
                {
                    savedMap = await connection.QueryFirstOrDefaultAsync<dynamic>(
                        "SELECT SyllabusData FROM MyCareerMap WHERE UserId = @UserId AND LOWER(TRIM(CareerName)) = LOWER(TRIM(@CareerName)) AND LOWER(TRIM(ModuleName)) = LOWER(TRIM(@ModuleName))",
                        new { UserId = userId, CareerName = targetCareer, ModuleName = moduleName }
                    );
                }

                if (savedMap != null && !string.IsNullOrEmpty((string)savedMap.SyllabusData))
                {
                    try
                    {
                        var savedSyllabus = JsonSerializer.Deserialize<SyllabusResponse>((string)savedMap.SyllabusData, new JsonSerializerOptions
                        {
                            PropertyNameCaseInsensitive = true
                        });
                        if (savedSyllabus != null && savedSyllabus.Topics != null && savedSyllabus.Topics.Count >= 1)
                        {
                            await PopulateCompletionStatusAsync(savedSyllabus, userId);
                            return Ok(savedSyllabus);
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error deserializing saved syllabus: {ex.Message}");
                    }
                }
            }

            SyllabusResponse syllabus;
            try 
            {
                var aiResponse = await _aiService.GetSyllabusAsync(moduleName, targetCareer);

                if (string.IsNullOrWhiteSpace(aiResponse))
                {
                    throw new Exception("Empty response from AI service.");
                }

                syllabus = JsonSerializer.Deserialize<SyllabusResponse>(aiResponse, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                }) ?? throw new Exception("Deserialization returned null.");

                if (userId > 0)
                {
                    try
                    {
                        using var connection = new SqlConnection(_connectionString);
                        var existingId = await connection.QueryFirstOrDefaultAsync<int?>(
                            "SELECT Id FROM MyCareerMap WHERE UserId = @UserId AND CareerName = @CareerName AND ModuleName = @ModuleName",
                            new { UserId = userId, CareerName = targetCareer, ModuleName = moduleName }
                        );

                        if (existingId != null)
                        {
                            await connection.ExecuteAsync(
                                "UPDATE MyCareerMap SET SyllabusData = @SyllabusData, CreatedAt = GETDATE() WHERE Id = @Id",
                                new { SyllabusData = aiResponse, Id = existingId.Value }
                            );
                        }
                        else
                        {
                            await connection.ExecuteAsync(
                                @"INSERT INTO MyCareerMap (UserId, CareerName, ModuleName, SyllabusData, CreatedAt) 
                                  VALUES (@UserId, @CareerName, @ModuleName, @SyllabusData, GETDATE())",
                                new { UserId = userId, CareerName = targetCareer, ModuleName = moduleName, SyllabusData = aiResponse }
                            );
                        }
                    }
                    catch (Exception dbEx)
                    {
                        Console.WriteLine($"Error caching AI syllabus to database: {dbEx.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Syllabus Generation Error (using fallback): {ex.Message}");
                // Fallback to a high-quality default syllabus if AI fails
                syllabus = new SyllabusResponse
                {
                    ModuleName = moduleName,
                    TargetCareer = targetCareer,
                    Topics = new List<SyllabusTopicDto>
                    {
                        new() { 
                            Id = 1,
                            Title = "Foundational Concepts", 
                            Description = $"Master the core principles and fundamental building blocks required for success as a {targetCareer}.", 
                            KeyTakeaways = new List<string> { "Core architecture", "Industry best practices", "Primary tools and workflows" },
                            EstimatedTime = "4h",
                            Difficulty = "Beginner"
                        },
                        new() { 
                            Id = 2,
                            Title = "Advanced Implementation", 
                            Description = "Deep dive into complex scenarios and high-level strategy implementation.", 
                            KeyTakeaways = new List<string> { "Optimization techniques", "Scalability patterns", "Security considerations" },
                            EstimatedTime = "6h",
                            Difficulty = "Advanced"
                        },
                        new() { 
                            Id = 3,
                            Title = "Project Lifecycle Management", 
                            Description = "Understanding the end-to-end delivery process from initial requirements to final deployment.", 
                            KeyTakeaways = new List<string> { "Stakeholder management", "Agile methodologies", "Quality assurance" },
                            EstimatedTime = "3h",
                            Difficulty = "Intermediate"
                        }
                    }
                };
            }

            if (userId > 0)
            {
                await PopulateCompletionStatusAsync(syllabus, userId);
            }

            return Ok(syllabus);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "Critical error in syllabus generation.", details = ex.Message });
        }
    }

    [HttpPost("save-career-map")]
    public async Task<IActionResult> SaveCareerMap([FromBody] SaveCareerMapRequestDto request)
    {
        try
        {
            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userIdStr) || !int.TryParse(userIdStr, out var userId))
            {
                return Unauthorized(new { message = "Invalid token claims." });
            }

            if (string.IsNullOrEmpty(request.CareerName) || string.IsNullOrEmpty(request.ModuleName) || request.SyllabusData == null)
            {
                return BadRequest(new { message = "CareerName, ModuleName, and SyllabusData are required." });
            }

            using var connection = new SqlConnection(_connectionString);
            
            // Check if it already exists
            var existingId = await connection.QueryFirstOrDefaultAsync<int?>(
                "SELECT Id FROM MyCareerMap WHERE UserId = @UserId AND CareerName = @CareerName AND ModuleName = @ModuleName",
                new { UserId = userId, CareerName = request.CareerName, ModuleName = request.ModuleName }
            );

            var syllabusJson = JsonSerializer.Serialize(request.SyllabusData);

            if (existingId != null)
            {
                // Update it
                await connection.ExecuteAsync(
                    "UPDATE MyCareerMap SET SyllabusData = @SyllabusData, CreatedAt = GETDATE() WHERE Id = @Id",
                    new { SyllabusData = syllabusJson, Id = existingId.Value }
                );
                return Ok(new { message = "Career map updated successfully.", id = existingId.Value });
            }
            else
            {
                // Insert it
                var newId = await connection.QuerySingleAsync<int>(
                    @"INSERT INTO MyCareerMap (UserId, CareerName, ModuleName, SyllabusData, CreatedAt) 
                      VALUES (@UserId, @CareerName, @ModuleName, @SyllabusData, GETDATE());
                      SELECT CAST(SCOPE_IDENTITY() as int);",
                    new { UserId = userId, CareerName = request.CareerName, ModuleName = request.ModuleName, SyllabusData = syllabusJson }
                );
                return Ok(new { message = "Career map saved successfully.", id = newId });
            }
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "Failed to save career map.", details = ex.Message });
        }
    }

    [HttpGet("saved-career-maps")]
    public async Task<IActionResult> GetSavedCareerMaps()
    {
        try
        {
            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userIdStr) || !int.TryParse(userIdStr, out var userId))
            {
                return Unauthorized(new { message = "Invalid token claims." });
            }

            using var connection = new SqlConnection(_connectionString);
            var sql = "SELECT Id, CareerName, ModuleName, CreatedAt FROM MyCareerMap WHERE UserId = @UserId ORDER BY CreatedAt DESC";
            var result = await connection.QueryAsync<SavedCareerMapDto>(sql, new { UserId = userId });
            return Ok(result);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "Failed to fetch saved career maps.", details = ex.Message });
        }
    }

    [HttpDelete("saved-career-maps/{id}")]
    public async Task<IActionResult> DeleteSavedCareerMap(int id)
    {
        try
        {
            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userIdStr) || !int.TryParse(userIdStr, out var userId))
            {
                return Unauthorized(new { message = "Invalid token claims." });
            }

            using var connection = new SqlConnection(_connectionString);
            var rowsAffected = await connection.ExecuteAsync(
                "DELETE FROM MyCareerMap WHERE Id = @Id AND UserId = @UserId",
                new { Id = id, UserId = userId }
            );

            if (rowsAffected == 0)
            {
                return NotFound(new { message = "Saved career map not found." });
            }

            return Ok(new { message = "Saved career map removed successfully." });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "Failed to delete saved career map.", details = ex.Message });
        }
    }

    private async Task PopulateCompletionStatusAsync(SyllabusResponse syllabus, int userId)
    {
        if (syllabus == null || syllabus.Topics == null || !syllabus.Topics.Any() || userId <= 0)
        {
            return;
        }

        try
        {
            using var connection = new SqlConnection(_connectionString);

            // 1. Fetch passed quizzes
            var passedQuizLessonIds = (await connection.QueryAsync<string>(
                "SELECT LessonId FROM LessonQuizResults WHERE UserId = @UserId AND Passed = 1",
                new { UserId = userId }
            )).Select(id => id.Trim()).ToHashSet(StringComparer.OrdinalIgnoreCase);

            // 2. Fetch completed topics
            var completedTopicTitles = (await connection.QueryAsync<string>(
                @"SELECT mt.Title FROM TopicProgress tp 
                  JOIN ModuleTopics mt ON tp.TopicId = mt.Id 
                  WHERE tp.UserId = @UserId AND tp.IsCompleted = 1",
                new { UserId = userId }
            )).Select(t => t.Trim()).ToHashSet(StringComparer.OrdinalIgnoreCase);

            var completedSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var id in passedQuizLessonIds) completedSet.Add(id);
            foreach (var title in completedTopicTitles) completedSet.Add(title);

            // Title normalization
            string Normalize(string s) => s.ToLower()
                                           .Replace(" ", "")
                                           .Replace("-", "")
                                           .Replace("/", "")
                                           .Replace("&", "and")
                                           .Trim();

            var normalizedCompletedSet = completedSet.Select(Normalize).ToHashSet();

            // 3. Populate completion status
            foreach (var topic in syllabus.Topics)
            {
                var title = topic.Title;
                var normalizedTitle = Normalize(title);

                bool isChapterCompleted = completedSet.Contains(title) || normalizedCompletedSet.Contains(normalizedTitle);
                topic.IsCompleted = isChapterCompleted;

                // Check for sub-syllabus in MyCareerMap for stage modules
                var underlyingMap = await connection.QueryFirstOrDefaultAsync<dynamic>(
                    "SELECT SyllabusData FROM MyCareerMap WHERE UserId = @UserId AND LOWER(TRIM(CareerName)) = LOWER(TRIM(@CareerName)) AND LOWER(TRIM(ModuleName)) = LOWER(TRIM(@ModuleName))",
                    new { UserId = userId, CareerName = syllabus.TargetCareer, ModuleName = title }
                );

                if (underlyingMap != null && !string.IsNullOrEmpty((string)underlyingMap.SyllabusData))
                {
                    try
                    {
                        var subSyllabus = JsonSerializer.Deserialize<SyllabusResponse>((string)underlyingMap.SyllabusData, new JsonSerializerOptions
                        {
                            PropertyNameCaseInsensitive = true
                        });

                        if (subSyllabus != null && subSyllabus.Topics != null && subSyllabus.Topics.Any())
                        {
                            int totalChapters = subSyllabus.Topics.Count;
                            int completedChapters = 0;

                            foreach (var subTopic in subSyllabus.Topics)
                            {
                                var subTitle = subTopic.Title;
                                var subNormalized = Normalize(subTitle);

                                if (completedSet.Contains(subTitle) || normalizedCompletedSet.Contains(subNormalized))
                                {
                                    completedChapters++;
                                }
                            }

                            topic.TotalChaptersCount = totalChapters;
                            topic.CompletedChaptersCount = completedChapters;
                            topic.IsCompleted = (completedChapters == totalChapters && totalChapters > 0);
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error calculating sub-syllabus progress for {title}: {ex.Message}");
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error populating syllabus completion status: {ex.Message}");
        }
    }
}

public class SaveCareerMapRequestDto
{
    public string CareerName { get; set; } = string.Empty;
    public string ModuleName { get; set; } = string.Empty;
    public object SyllabusData { get; set; } = null!;
}

public class SavedCareerMapDto
{
    public int Id { get; set; }
    public string CareerName { get; set; } = string.Empty;
    public string ModuleName { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}
}

using System.Security.Claims;
using System.Collections.Generic;
using GuidYu_API.DTOs;
using GuidYu_API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class CareerPathController : ControllerBase
{
    private readonly ICareerPathService _careerPathService;

    public CareerPathController(ICareerPathService careerPathService)
    {
        _careerPathService = careerPathService;
    }

    [HttpGet("overview")]
    public async Task<IActionResult> GetOverview([FromQuery] string? targetCareer, [FromQuery] string? stage = "Beginner")
    {
        var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!int.TryParse(userIdStr, out var userId)) return Unauthorized();

        var result = await _careerPathService.GetCareerPathOverviewAsync(userId, targetCareer, stage);
        if (result == null)
        {
            // Return an empty overview to avoid JSON parsing errors on the frontend
            var emptyResult = new CareerPathOverviewDto
            {
                Summary = new CareerPathSummaryDto
                {
                    CurrentRole = "Professional",
                    TargetRole = targetCareer ?? "Target Role",
                    MatchPercentage = 0,
                    EstimatedTime = "0"
                },
                Journey = new List<CareerPathStepDto>(),
                Skills = new List<CareerPathSkillDto>(),
                Insights = new List<CareerPathInsightDto>()
            };
            return Ok(emptyResult);
        }
        return Ok(result);
    }

    [HttpPost("save")]
    public async Task<IActionResult> SaveCareerPath([FromBody] GuidYu_API.DTOs.CareerPathOverviewDto data)
    {
        var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!int.TryParse(userIdStr, out var userId)) return Unauthorized();

        if (data == null) return BadRequest("Invalid career path data.");

        var success = await _careerPathService.SaveCareerPathAsync(userId, data);
        if (success)
            return Ok(new { Message = "Career path saved successfully." });

        return StatusCode(500, "Failed to save career path.");
    }

    /// <summary>
    /// Lightweight endpoint: saves just the selected career name as the user's goal.
    /// Builds a minimal DTO so the client doesn't need to send a full CareerPathOverviewDto.
    /// </summary>
    [HttpPost("select")]
    public async Task<IActionResult> SelectCareer([FromQuery] string targetCareer)
    {
        var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!int.TryParse(userIdStr, out var userId)) return Unauthorized();

        if (string.IsNullOrWhiteSpace(targetCareer))
            return BadRequest("Career name is required.");

        // Generate the full career path overview (which internally saves it to the database with all details)
        var data = await _careerPathService.GetCareerPathOverviewAsync(userId, targetCareer);
        if (data != null)
        {
            // Explicitly save to guarantee full DB representation of overview and user details (CareerGoal)
            await _careerPathService.SaveCareerPathAsync(userId, data);
            return Ok(new { Message = "Career selected and saved successfully.", TargetCareer = targetCareer });
        }

        return StatusCode(500, "Failed to select career.");
    }
}

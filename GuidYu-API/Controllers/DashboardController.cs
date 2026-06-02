using System.Security.Claims;
using GuidYu_API.DTOs;
using GuidYu_API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GuidYu_API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class DashboardController : ControllerBase
{
    private readonly IDashboardService _dashboardService;

    public DashboardController(IDashboardService dashboardService)
    {
        _dashboardService = dashboardService;
    }

    private int GetUserId()
    {
        var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return int.TryParse(userIdStr, out var userId) ? userId : 0;
    }

    [HttpGet("overview")]
    public async Task<IActionResult> GetOverview()
    {
        var userId = GetUserId();
        if (userId == 0) return Unauthorized();
        var result = await _dashboardService.GetOverviewAsync(userId);
        return Ok(result);
    }

    [HttpGet("metrics")]
    public async Task<IActionResult> GetMetrics()
    {
        var userId = GetUserId();
        if (userId == 0) return Unauthorized();
        var result = await _dashboardService.GetMetricsAsync(userId);
        return Ok(result);
    }

    [HttpGet("progress")]
    public async Task<IActionResult> GetProgress()
    {
        var userId = GetUserId();
        if (userId == 0) return Unauthorized();
        var result = await _dashboardService.GetProgressAsync(userId);
        return Ok(result);
    }

    [HttpGet("next-steps")]
    public async Task<IActionResult> GetNextSteps()
    {
        var userId = GetUserId();
        if (userId == 0) return Unauthorized();
        var result = await _dashboardService.GetNextStepsAsync(userId);
        return Ok(result);
    }

    [HttpGet("insights")]
    public async Task<IActionResult> GetInsights()
    {
        var userId = GetUserId();
        if (userId == 0) return Unauthorized();
        var result = await _dashboardService.GetInsightsAsync(userId);
        return Ok(result);
    }

    [HttpGet("roadmap")]
    public async Task<IActionResult> GetRoadmap()
    {
        var userId = GetUserId();
        if (userId == 0) return Unauthorized();
        var result = await _dashboardService.GetRoadmapAsync(userId);
        return Ok(result);
    }

    [HttpGet("activity")]
    public async Task<IActionResult> GetActivity()
    {
        var userId = GetUserId();
        if (userId == 0) return Unauthorized();
        var result = await _dashboardService.GetActivityAsync(userId);
        return Ok(result);
    }
}

using System.Security.Claims;
using GuidYu_API.DTOs;
using GuidYu_API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GuidYu_API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ResumeController : ControllerBase
{
    private readonly IResumeService _resumeService;

    public ResumeController(IResumeService resumeService)
    {
        _resumeService = resumeService;
    }

    private int GetUserId()
    {
        var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return int.TryParse(userIdStr, out var userId) ? userId : 0;
    }

    [HttpGet("my-resume")]
    public async Task<IActionResult> GetMyResume()
    {
        var userId = GetUserId();
        if (userId == 0) return Unauthorized();

        var resume = await _resumeService.GetMyResumeAsync(userId);
        if (resume == null) return NotFound(new { message = "Resume not found." });

        return Ok(resume);
    }

    [HttpPost]
    public async Task<IActionResult> CreateResume([FromBody] CreateResumeDto createDto)
    {
        var userId = GetUserId();
        if (userId == 0) return Unauthorized();

        // Check if resume already exists for this user
        var existing = await _resumeService.GetMyResumeAsync(userId);
        if (existing != null) return BadRequest(new { message = "Resume already exists for this user. Use PUT to update." });

        var result = await _resumeService.CreateResumeAsync(userId, createDto);
        return Ok(result);
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateResume(int id, [FromBody] UpdateResumeDto updateDto)
    {
        var userId = GetUserId();
        if (userId == 0) return Unauthorized();

        var result = await _resumeService.UpdateResumeAsync(userId, id, updateDto);
        if (result == null) return NotFound(new { message = "Resume not found or access denied." });

        return Ok(result);
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteResume(int id)
    {
        var userId = GetUserId();
        if (userId == 0) return Unauthorized();

        var result = await _resumeService.DeleteResumeAsync(userId, id);
        if (!result) return NotFound(new { message = "Resume not found or access denied." });

        return NoContent();
    }

    [HttpGet("download")]
    public async Task<IActionResult> DownloadResume()
    {
        var userId = GetUserId();
        if (userId == 0) return Unauthorized();

        var pdfBytes = await _resumeService.GeneratePdfAsync(userId);
        if (pdfBytes == null || pdfBytes.Length == 0) return NotFound(new { message = "Resume not found." });

        return File(pdfBytes, "application/pdf", "Resume.pdf");
    }
}

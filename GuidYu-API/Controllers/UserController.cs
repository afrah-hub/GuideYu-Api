using System.Security.Claims;
using GuidYu_API.DTOs;
using GuidYu_API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using GuidYu_API.Repositories;

namespace GuidYu_API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class UserController : ControllerBase
{
    private readonly IAuthService _authService;
    private readonly IPhotoService _photoService;
    private readonly IUserRepository _userRepository;

    public UserController(IAuthService authService, IPhotoService photoService, IUserRepository userRepository)
    {
        _authService = authService;
        _photoService = photoService;
        _userRepository = userRepository;
    }

    [HttpGet("me")]
    public async Task<IActionResult> GetProfile()
    {
        var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userIdStr) || !int.TryParse(userIdStr, out var userId))
        {
            return Unauthorized(new { message = "Invalid token claims." });
        }

        var user = await _authService.GetProfileAsync(userId);
        if (user == null)
        {
            return NotFound(new { message = "User not found." });
        }

        return Ok(new
        {
            user.Id,
            user.FullName,
            user.Email,
            user.HighestQualification,
            user.Stream,
            user.InstitutionName,
            user.CurrentStatus,
            user.CareerGoal,
            user.PreferredIndustry,
            user.Skills,
            user.SkillLevels,
            user.Interests,
            user.ProfileImageUrl,
            user.Role
        });
    }

    [HttpPut("profile")]
    public async Task<IActionResult> UpdateProfile([FromForm] UpdateProfileRequest request)
    {
        var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userIdStr) || !int.TryParse(userIdStr, out var userId))
        {
            return Unauthorized(new { message = "Invalid token claims." });
        }

        var result = await _authService.UpdateProfileAsync(userId, request);
        if (result.Success)
        {
            return Ok(new { message = result.Message });
        }

        return BadRequest(new { message = result.Message });
    }

    [HttpPost("change-password")]
    public async Task<IActionResult> ChangePassword([FromForm] ChangePasswordRequest request)
    {
        var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userIdStr) || !int.TryParse(userIdStr, out var userId))
        {
            return Unauthorized(new { message = "Invalid token claims." });
        }

        var result = await _authService.ChangePasswordAsync(userId, request);
        if (result.Success)
        {
            return Ok(new { message = result.Message });
        }

        return BadRequest(new { message = result.Message });
    }

    [HttpPost("upload-photo")]
    public async Task<IActionResult> UploadPhoto(IFormFile file)
    {
        var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userIdStr) || !int.TryParse(userIdStr, out var userId))
        {
            return Unauthorized(new { message = "Invalid token claims." });
        }

        var result = await _photoService.AddPhotoAsync(file);

        if (result.Error != null)
        {
            return BadRequest(new { message = result.Error.Message });
        }

        await _userRepository.UpdateProfileImageAsync(userId, result.SecureUrl.AbsoluteUri);

        return Ok(new { 
            message = "Photo uploaded successfully", 
            url = result.SecureUrl.AbsoluteUri 
        });
    }

    [HttpDelete]
    public async Task<IActionResult> DeleteAccount()
    {
        var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userIdStr) || !int.TryParse(userIdStr, out var userId))
        {
            return Unauthorized(new { message = "Invalid token claims." });
        }

        var result = await _authService.DeleteAccountAsync(userId);
        if (result.Success)
        {
            Response.Cookies.Delete("jwt");
            Response.Cookies.Delete("refreshToken");
            return Ok(new { message = result.Message });
        }

        return BadRequest(new { message = result.Message });
    }
}

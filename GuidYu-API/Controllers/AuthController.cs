using GuidYu_API.DTOs;
using GuidYu_API.Services;
using Microsoft.AspNetCore.Mvc;

namespace GuidYu_API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;

    public AuthController(IAuthService authService)
    {
        _authService = authService;
    }

    [HttpPost("register")]
    public async Task<IActionResult> Register([FromForm] RegisterRequest request)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var result = await _authService.RegisterAsync(request);
        if (result.Success)
        {
            return Ok(new { message = result.Message });
        }

        return BadRequest(new { message = result.Message });
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromForm] LoginRequest request)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var result = await _authService.LoginAsync(request);
        if (result.Success)
        {
            SetTokenCookies(result.Token, result.RefreshToken);

            return Ok(new
            {
                result.User!.Id,
                result.User.FullName,
                result.User.Email,
                result.User.Role,
                result.Token,
                result.RefreshToken,
            });
        }

        return Unauthorized(new { message = result.Message });
    }

    [HttpPost("google")]
    public async Task<IActionResult> GoogleLogin([FromForm] SocialAuthRequest request)
    {
        var result = await _authService.GoogleLoginAsync(request);
        if (result.Success)
        {
            SetTokenCookies(result.Token, result.RefreshToken);
            return Ok(new
            {
                user = new 
                {
                    result.User!.Id,
                    result.User.FullName,
                    result.User.Email,
                    result.User.Role,
                    isNewUser = result.IsNewUser
                },
                result.Token,
                result.RefreshToken,
            });
        }
        return Unauthorized(new { message = result.Message });
    }

    [HttpPost("apple")]
    public async Task<IActionResult> AppleLogin([FromForm] SocialAuthRequest request)
    {
        var result = await _authService.AppleLoginAsync(request);
        if (result.Success)
        {
            SetTokenCookies(result.Token, result.RefreshToken);
            return Ok(new
            {
                user = new 
                {
                    result.User!.Id,
                    result.User.FullName,
                    result.User.Email,
                    result.User.Role,
                    isNewUser = result.IsNewUser
                },
                result.Token,
                result.RefreshToken,
            });
        }
        return Unauthorized(new { message = result.Message });
    }

    [HttpPost("refresh")]
    public async Task<IActionResult> RefreshToken()
    {
        var refreshToken = Request.Cookies["refreshToken"];
        if (string.IsNullOrEmpty(refreshToken))
        {
            return Unauthorized(new { message = "Refresh token is missing." });
        }

        var result = await _authService.RefreshTokenAsync(refreshToken);
        if (result.Success)
        {
            SetTokenCookies(result.Token, result.RefreshToken);
            return Ok(new { message = "Token refreshed successfully." });
        }

        return Unauthorized(new { message = result.Message });
    }

    [HttpPost("forgot-password")]
    public async Task<IActionResult> ForgotPassword([FromForm] ForgotPasswordRequest request)
    {
        var result = await _authService.ForgotPasswordAsync(request);
        return Ok(new { message = result.Message, token = result.Token });
    }

    [HttpPost("reset-password")]
    public async Task<IActionResult> ResetPassword([FromForm] ResetPasswordRequest request)
    {
        var result = await _authService.ResetPasswordAsync(request);
        if (result.Success)
        {
            return Ok(new { message = result.Message });
        }
        return BadRequest(new { message = result.Message });
    }

    [HttpPost("logout")]
    public async Task<IActionResult> Logout()
    {
        var refreshToken = Request.Cookies["refreshToken"];
        if (!string.IsNullOrEmpty(refreshToken))
        {
            await _authService.LogoutAsync(refreshToken);
        }

        var cookieOptions = new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.None,
            Expires = DateTime.UtcNow.AddDays(-1),
            Path = "/"
        };

        Response.Cookies.Delete("token", cookieOptions);
        Response.Cookies.Delete("refreshToken", cookieOptions);
        
        return Ok(new { message = "Logged out successfully." });
    }

    private void SetTokenCookies(string token, string refreshToken)
    {
        var cookieOptions = new CookieOptions
        {
            HttpOnly = true,
            Secure = true, 
            SameSite = SameSiteMode.None,
            Expires = DateTime.UtcNow.AddDays(7),
            Path = "/"
        };

        Response.Cookies.Append("token", token, cookieOptions);
        Response.Cookies.Append("refreshToken", refreshToken, cookieOptions);
    }
}

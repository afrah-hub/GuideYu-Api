namespace GuidYu_API.Services;

using GuidYu_API.DTOs;
using GuidYu_API.Models;

public interface IAuthService
{
    Task<(bool Success, string Message)> RegisterAsync(RegisterRequest request);
    Task<(bool Success, string Message, User? User, string Token, string RefreshToken)> LoginAsync(LoginRequest request);
    Task<(bool Success, string Message, User? User, string Token, string RefreshToken, bool IsNewUser)> GoogleLoginAsync(SocialAuthRequest request);
    Task<(bool Success, string Message, User? User, string Token, string RefreshToken, bool IsNewUser)> AppleLoginAsync(SocialAuthRequest request);
    Task<(bool Success, string Message, string Token, string RefreshToken)> RefreshTokenAsync(string refreshToken);
    Task<(bool Success, string Message)> UpdateProfileAsync(int userId, UpdateProfileRequest request);
    Task<(bool Success, string Message)> ChangePasswordAsync(int userId, ChangePasswordRequest request);
    Task<(bool Success, string Message, string? Token)> ForgotPasswordAsync(ForgotPasswordRequest request);
    Task<(bool Success, string Message)> ResetPasswordAsync(ResetPasswordRequest request);
    Task<(bool Success, string Message)> DeleteAccountAsync(int userId);
    Task<User?> GetProfileAsync(int userId);
    Task LogoutAsync(string refreshToken);
}

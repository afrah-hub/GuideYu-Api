namespace GuidYu_API.Repositories;

using GuidYu_API.Models;

public interface IUserRepository
{
    Task<int> RegisterUserAsync(User user);
    Task<User?> GetUserByEmailAsync(string email);
    Task<User?> GetUserByIdAsync(int id);
    Task<int> UpdateLastLoginAsync(int id);
    Task<int> UpdateUserProfileAsync(User user);
    Task<int> UpdateRefreshTokenAsync(int userId, string? refreshToken, DateTime? expiryTime);
    Task<User?> GetUserByRefreshTokenAsync(string refreshToken);
    Task<User?> GetUserByGoogleIdAsync(string googleId);
    Task<User?> GetUserByAppleIdAsync(string appleId);
    Task<int> UpdateGoogleIdAsync(int userId, string googleId);
    Task<int> UpdateProfileImageAsync(int userId, string imageUrl);
    Task<int> UpdatePasswordAsync(int userId, string passwordHash);
    Task<int> DeleteUserAsync(int userId);
}

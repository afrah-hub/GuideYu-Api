using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using BCrypt.Net;
using GuidYu_API.DTOs;
using GuidYu_API.Models;
using GuidYu_API.Repositories;
using Microsoft.IdentityModel.Tokens;
using Google.Apis.Auth;

namespace GuidYu_API.Services;

public class AuthService : IAuthService
{
    private readonly IUserRepository _userRepository;
    private readonly IConfiguration _configuration;

    public AuthService(IUserRepository userRepository, IConfiguration configuration)
    {
        _userRepository = userRepository;
        _configuration = configuration;
    }

    public async Task<(bool Success, string Message)> RegisterAsync(RegisterRequest request)
    {
        var existingUser = await _userRepository.GetUserByEmailAsync(request.Email);
        if (existingUser != null)
        {
            return (false, "Email is already in use.");
        }

        if (string.IsNullOrWhiteSpace(request.FullName) || request.FullName.Trim().Length < 3)
        {
            return (false, "Full Name must be at least 3 characters long.");
        }

        request.Email = request.Email.Trim().ToLower();
        try
        {
            var addr = new System.Net.Mail.MailAddress(request.Email);
            if (addr.Address != request.Email) throw new Exception();
        }
        catch
        {
            return (false, "Please enter a valid email address.");
        }

        if (request.Password.Length < 8)
        {
            return (false, "Password must be at least 8 characters long.");
        }

        if (!System.Text.RegularExpressions.Regex.IsMatch(request.Password, @"[!@#$%^&*(),.?\x22:{}|<>]"))
        {
            return (false, "Password must contain at least one special character.");
        }

        var user = new User
        {
            FullName = request.FullName,
            Email = request.Email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
            CreatedAt = DateTime.UtcNow,
            IsEmailVerified = false,
            Role = "User",
            AuthProvider = "Manual"
        };

        var rowsAffected = await _userRepository.RegisterUserAsync(user);
        if (rowsAffected > 0)
        {
            return (true, "Registration successful.");
        }

        return (false, "Failed to register user.");
    }

    public async Task<(bool Success, string Message, User? User, string Token, string RefreshToken)> LoginAsync(LoginRequest request)
    {
        var user = await _userRepository.GetUserByEmailAsync(request.Email);
        if (user == null)
        {
            return (false, "No account exists with this email address.", null, string.Empty, string.Empty);
        }

        if (user.IsBlocked)
        {
            return (false, "Access restricted. This account has been blocked by an administrator.", null, string.Empty, string.Empty);
        }

        try 
        {
            if (user.PasswordHash.StartsWith("SOCIAL_LOGIN_") || !BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
            {
                var message = user.PasswordHash.StartsWith("SOCIAL_LOGIN_") 
                    ? $"This account is linked with {user.AuthProvider}. Please login using {user.AuthProvider}." 
                    : "Invalid email or password.";
                return (false, message, null, string.Empty, string.Empty);
            }
        }
        catch (BCrypt.Net.SaltParseException)
        {
            return (false, "Invalid account state or login method. Please use social login if applicable.", null, string.Empty, string.Empty);
        }

        await _userRepository.UpdateLastLoginAsync(user.Id);

        var token = GenerateJwtToken(user);
        var refreshToken = GenerateRefreshToken();
        
        await _userRepository.UpdateRefreshTokenAsync(user.Id, refreshToken, DateTime.UtcNow.AddDays(7));

        return (true, "Login successful.", user, token, refreshToken);
    }

    public async Task<(bool Success, string Message, User? User, string Token, string RefreshToken, bool IsNewUser)> GoogleLoginAsync(SocialAuthRequest request)
    {
        try
        {
            string? email = null;
            string? name = null;
            string? subject = null;

            try
            {
                var payload = await GoogleJsonWebSignature.ValidateAsync(request.Token);
                email = payload.Email;
                name = payload.Name;
                subject = payload.Subject;
            }
            catch (Exception valEx)
            {
                try 
                {
                    using var client = new HttpClient();
                    client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", request.Token);
                    var userInfoResponse = await client.GetAsync("https://www.googleapis.com/oauth2/v3/userinfo");
                    
                    if (userInfoResponse.IsSuccessStatusCode)
                    {
                        var content = await userInfoResponse.Content.ReadAsStringAsync();
                        var userInfo = System.Text.Json.JsonDocument.Parse(content).RootElement;
                        
                        if (userInfo.TryGetProperty("email", out var emailProp)) email = emailProp.GetString();
                        if (userInfo.TryGetProperty("name", out var nameProp)) name = nameProp.GetString();
                        if (userInfo.TryGetProperty("sub", out var subProp)) subject = subProp.GetString();
                    }
                    else
                    {
                        var errorDetails = await userInfoResponse.Content.ReadAsStringAsync();
                        return (false, $"Google verification failed. Token is neither a valid ID Token nor a valid Access Token. Google response: {errorDetails}", null, string.Empty, string.Empty, false);
                    }
                }
                catch (Exception httpEx)
                {
                    return (false, $"Network error while verifying Google token: {httpEx.Message}. (Original ID Token error: {valEx.Message})", null, string.Empty, string.Empty, false);
                }
            }
 
            if (string.IsNullOrEmpty(email)) return (false, "Google account does not have a public email address.", null, string.Empty, string.Empty, false);
            if (string.IsNullOrEmpty(subject)) return (false, "Google account is missing a unique Subject ID.", null, string.Empty, string.Empty, false);
 
            bool isNewUser = false;
            var user = await _userRepository.GetUserByGoogleIdAsync(subject);
            
            if (user == null)
            {
                user = await _userRepository.GetUserByEmailAsync(email);
                
                if (user != null)
                {
                    user.GoogleId = subject;
                    user.AuthProvider = "Google";
                    await _userRepository.UpdateGoogleIdAsync(user.Id, subject);
                }
                else
                {
                    isNewUser = true;
                    user = new User
                    {
                        FullName = name ?? "Google User",
                        Email = email,
                        GoogleId = subject,
                        AuthProvider = "Google",
                        PasswordHash = "SOCIAL_LOGIN_" + Guid.NewGuid().ToString(),
                        CreatedAt = DateTime.UtcNow,
                        IsEmailVerified = true,
                        Role = "User"
                    };
                    
                    try 
                    {
                        await _userRepository.RegisterUserAsync(user);
                        user = await _userRepository.GetUserByEmailAsync(email);
                    }
                    catch (Exception dbEx)
                    {
                        return (false, $"Account creation failed: {dbEx.Message}", null, string.Empty, string.Empty, false);
                    }
                }
            }
 
            if (user == null) return (false, "Authentication succeeded but your local profile could not be retrieved.", null, string.Empty, string.Empty, false);
 
            if (user.IsBlocked)
            {
                return (false, "Access restricted. Your account has been blocked by an administrator.", null, string.Empty, string.Empty, false);
            }

            await _userRepository.UpdateLastLoginAsync(user.Id);
            var token = GenerateJwtToken(user);
            var refreshToken = GenerateRefreshToken();
            await _userRepository.UpdateRefreshTokenAsync(user.Id, refreshToken, DateTime.UtcNow.AddDays(7));
 
            return (true, "Google login successful.", user, token, refreshToken, isNewUser);
        }
        catch (Exception ex)
        {
            return (false, $"Internal Error during Google Auth: {ex.Message}", null, string.Empty, string.Empty, false);
        }
    }
 
    public async Task<(bool Success, string Message, User? User, string Token, string RefreshToken, bool IsNewUser)> AppleLoginAsync(SocialAuthRequest request)
    {
        
        return (false, "Apple Login implementation requires your Apple Developer credentials.", null, string.Empty, string.Empty, false);
    }

    public async Task<(bool Success, string Message, string Token, string RefreshToken)> RefreshTokenAsync(string refreshToken)
    {
        var user = await _userRepository.GetUserByRefreshTokenAsync(refreshToken);
        if (user == null || user.RefreshTokenExpiryTime <= DateTime.UtcNow)
        {
            return (false, "Invalid or expired refresh token.", string.Empty, string.Empty);
        }

        if (user.IsBlocked)
        {
            return (false, "Access restricted. Account is blocked.", string.Empty, string.Empty);
        }

        var newAccessToken = GenerateJwtToken(user);
        var newRefreshToken = GenerateRefreshToken();

        await _userRepository.UpdateRefreshTokenAsync(user.Id, newRefreshToken, DateTime.UtcNow.AddDays(7));

        return (true, "Token refreshed successfully.", newAccessToken, newRefreshToken);
    }

    public async Task<(bool Success, string Message)> UpdateProfileAsync(int userId, UpdateProfileRequest request)
    {
        var user = await _userRepository.GetUserByIdAsync(userId);
        if (user == null)
        {
            return (false, "User not found.");
        }

        if (request.FullName != null)
        {
            user.FullName = request.FullName;
        }
        user.HighestQualification = request.HighestQualification;
        user.Stream = request.Stream;
        user.InstitutionName = request.InstitutionName;
        user.CurrentStatus = request.CurrentStatus;
        user.CareerGoal = request.CareerGoal;
        user.PreferredIndustry = request.PreferredIndustry;
        user.Skills = request.Skills;
        user.SkillLevels = request.SkillLevels;
        user.Interests = request.Interests;

        var rowsAffected = await _userRepository.UpdateUserProfileAsync(user);
        if (rowsAffected > 0)
        {
            return (true, "Profile updated successfully.");
        }

        return (false, "Failed to update profile.");
    }

    public async Task<(bool Success, string Message)> ChangePasswordAsync(int userId, ChangePasswordRequest request)
    {
        var user = await _userRepository.GetUserByIdAsync(userId);
        if (user == null)
        {
            return (false, "User not found.");
        }

        if (user.PasswordHash.StartsWith("SOCIAL_LOGIN_"))
        {
            return (false, $"This account is linked with {user.AuthProvider}. Password changes are not applicable for social accounts.");
        }

        if (!BCrypt.Net.BCrypt.Verify(request.CurrentPassword, user.PasswordHash))
        {
            return (false, "Current password is incorrect.");
        }

        // Hash and update new password
        var newPasswordHash = BCrypt.Net.BCrypt.HashPassword(request.NewPassword);
        var rowsAffected = await _userRepository.UpdatePasswordAsync(userId, newPasswordHash);

        if (rowsAffected > 0)
        {
            return (true, "Password changed successfully.");
        }

        return (false, "Failed to update password.");
    }

    public async Task<(bool Success, string Message, string? Token)> ForgotPasswordAsync(ForgotPasswordRequest request)
    {
        if (request == null) return (false, "Invalid request.", null);
        var email = request.Email?.Trim().ToLower();
        if (string.IsNullOrEmpty(email)) return (false, "Email is required.", null);

        var user = await _userRepository.GetUserByEmailAsync(email);
        
        // Security best practice: Don't reveal if the email exists
        if (user == null)
        {
            return (true, "If an account exists with this email, an OTP has been sent.", null);
        }

        // Generate 6-digit OTP
        var otp = new Random().Next(100000, 999999).ToString();

        // Generate Stateless Token (JWT) containing the hashed OTP
        var token = GenerateResetToken(user, otp);

        // We return "token|otp" so the frontend can:
        // 1. Store the token for the final reset call
        // 2. Send the OTP to the user via EmailJS
        return (true, "OTP generated successfully.", $"{token}|{otp}");
    }

    public async Task<(bool Success, string Message)> ResetPasswordAsync(ResetPasswordRequest request)
    {
        try 
        {
            var tokenHandler = new JwtSecurityTokenHandler();
            var key = Encoding.UTF8.GetBytes(_configuration["Jwt:Key"]!);
            tokenHandler.ValidateToken(request.Token, new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(key),
                ValidateIssuer = true,
                ValidIssuer = _configuration["Jwt:Issuer"],
                ValidateAudience = true,
                ValidAudience = _configuration["Jwt:Audience"],
                ClockSkew = TimeSpan.Zero
            }, out SecurityToken validatedToken);

            var jwtToken = (JwtSecurityToken)validatedToken;
            
            // Verify OTP Hash from Token
            var otpHash = jwtToken.Claims.FirstOrDefault(x => x.Type == "OtpHash")?.Value;
            if (string.IsNullOrEmpty(otpHash) || !BCrypt.Net.BCrypt.Verify(request.Otp, otpHash))
            {
                return (false, "Invalid or expired OTP code.");
            }

            var userIdClaim = jwtToken.Claims.FirstOrDefault(x => x.Type == JwtRegisteredClaimNames.Sub) 
                             ?? jwtToken.Claims.FirstOrDefault(x => x.Type == ClaimTypes.NameIdentifier);
            
            if (userIdClaim == null) return (false, "Invalid token payload.");
            
            var userId = int.Parse(userIdClaim.Value);

            var user = await _userRepository.GetUserByIdAsync(userId);
            if (user == null)
            {
                return (false, "User not found.");
            }

            // Hash and update new password
            var newPasswordHash = BCrypt.Net.BCrypt.HashPassword(request.NewPassword);
            await _userRepository.UpdatePasswordAsync(user.Id, newPasswordHash);

            return (true, "Password has been reset successfully.");
        }
        catch (Exception)
        {
            return (false, "Invalid or expired reset session.");
        }
    }

    private string GenerateResetToken(User user, string otp)
    {
        var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_configuration["Jwt:Key"]!));
        var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);

        // Hash the OTP before putting it in the token for security
        var otpHash = BCrypt.Net.BCrypt.HashPassword(otp);

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(JwtRegisteredClaimNames.Email, user.Email),
            new Claim("OtpHash", otpHash),
            new Claim("Purpose", "PasswordReset")
        };

        var token = new JwtSecurityToken(
            issuer: _configuration["Jwt:Issuer"],
            audience: _configuration["Jwt:Audience"],
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(15), // OTPs usually have shorter life
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public async Task<(bool Success, string Message)> DeleteAccountAsync(int userId)
    {
        var rowsAffected = await _userRepository.DeleteUserAsync(userId);
        if (rowsAffected > 0)
        {
            return (true, "Account deleted successfully.");
        }
        return (false, "Failed to delete account.");
    }

    public async Task<User?> GetProfileAsync(int userId)
    {
        return await _userRepository.GetUserByIdAsync(userId);
    }
    
    public async Task LogoutAsync(string refreshToken)
    {
        var user = await _userRepository.GetUserByRefreshTokenAsync(refreshToken);
        if (user != null)
        {
            await _userRepository.UpdateRefreshTokenAsync(user.Id, null, null);
        }
    }

    private string GenerateJwtToken(User user)
    {
        var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_configuration["Jwt:Key"]!));
        var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(JwtRegisteredClaimNames.Email, user.Email),
            new Claim(ClaimTypes.Email, user.Email),
            new Claim("FullName", user.FullName),
            new Claim(ClaimTypes.Role, user.Role),
            new Claim("role", user.Role)
        };

        var token = new JwtSecurityToken(
            issuer: _configuration["Jwt:Issuer"],
            audience: _configuration["Jwt:Audience"],
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(120), // extended to 2 hours
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private string GenerateRefreshToken()
    {
        var randomNumber = new byte[32];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(randomNumber);
        return Convert.ToBase64String(randomNumber);
    }
}

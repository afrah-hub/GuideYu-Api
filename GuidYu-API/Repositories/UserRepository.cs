using System.Data;
using Dapper;
using GuidYu_API.Models;
using Microsoft.Data.SqlClient;

namespace GuidYu_API.Repositories;

public class UserRepository : IUserRepository
{
    private readonly string _connectionString;

    public UserRepository(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("DefaultConnection") 
            ?? throw new InvalidOperationException("DefaultConnection string not found.");
    }

    private IDbConnection CreateConnection()
    {
        return new SqlConnection(_connectionString);
    }

    public async Task<int> RegisterUserAsync(User user)
    {
        using var connection = CreateConnection();
        connection.Open();
        using var transaction = connection.BeginTransaction();
        try
        {
            const string sql = @"
                INSERT INTO Users (FullName, Email, PasswordHash, CreatedAt, IsEmailVerified, Role, GoogleId, AppleId, AuthProvider)
                VALUES (@FullName, @Email, @PasswordHash, @CreatedAt, @IsEmailVerified, @Role, @GoogleId, @AppleId, @AuthProvider);
                SELECT CAST(SCOPE_IDENTITY() as int);
            ";

            var userId = await connection.QuerySingleAsync<int>(sql, new
            {
                user.FullName,
                user.Email,
                user.PasswordHash,
                CreatedAt = user.CreatedAt == default ? DateTime.UtcNow : user.CreatedAt,
                user.IsEmailVerified,
                Role = user.Role ?? "User",
                user.GoogleId,
                user.AppleId,
                user.AuthProvider
            }, transaction);

            await connection.ExecuteAsync("INSERT INTO UserDetails (UserId) VALUES (@UserId)", new { UserId = userId }, transaction);

            transaction.Commit();
            return 1;
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }

    public async Task<User?> GetUserByEmailAsync(string email)
    {
        using var connection = CreateConnection();
        const string sql = @"
            SELECT u.*, ud.*
            FROM Users u
            LEFT JOIN UserDetails ud ON u.Id = ud.UserId
            WHERE u.Email = @Email";
        
        return await connection.QuerySingleOrDefaultAsync<User>(sql, new { Email = email });
    }

    public async Task<User?> GetUserByIdAsync(int id)
    {
        using var connection = CreateConnection();
        const string sql = @"
            SELECT u.*, ud.*
            FROM Users u
            LEFT JOIN UserDetails ud ON u.Id = ud.UserId
            WHERE u.Id = @Id";
            
        return await connection.QuerySingleOrDefaultAsync<User>(sql, new { Id = id });
    }

    public async Task<int> UpdateLastLoginAsync(int id)
    {
        using var connection = CreateConnection();
        const string sql = "UPDATE Users SET LastLoginAt = @LastLoginAt WHERE Id = @Id";
        return await connection.ExecuteAsync(sql, new { Id = id, LastLoginAt = DateTime.UtcNow });
    }

    public async Task<int> UpdateUserProfileAsync(User user)
    {
        using var connection = CreateConnection();
        connection.Open();
        using var transaction = connection.BeginTransaction();
        try
        {
            await connection.ExecuteAsync("UPDATE Users SET FullName = @FullName WHERE Id = @Id", new { user.Id, user.FullName }, transaction);

            const string detailsSql = @"
                MERGE INTO UserDetails AS target
                USING (SELECT @UserId AS UserId) AS source
                ON (target.UserId = source.UserId)
                WHEN MATCHED THEN
                    UPDATE SET 
                        HighestQualification = @HighestQualification, 
                        Stream = @Stream, 
                        InstitutionName = @InstitutionName,
                        CurrentStatus = @CurrentStatus,
                        CareerGoal = @CareerGoal,
                        PreferredIndustry = @PreferredIndustry,
                        Skills = @Skills,
                        SkillLevels = @SkillLevels,
                        Interests = @Interests
                WHEN NOT MATCHED THEN
                    INSERT (UserId, HighestQualification, Stream, InstitutionName, CurrentStatus, CareerGoal, PreferredIndustry, Skills, SkillLevels, Interests)
                    VALUES (@UserId, @HighestQualification, @Stream, @InstitutionName, @CurrentStatus, @CareerGoal, @PreferredIndustry, @Skills, @SkillLevels, @Interests);";
            
            await connection.ExecuteAsync(detailsSql, new { 
                UserId = user.Id, 
                user.HighestQualification, 
                user.Stream, 
                user.InstitutionName,
                user.CurrentStatus,
                user.CareerGoal,
                user.PreferredIndustry,
                user.Skills,
                user.SkillLevels,
                user.Interests
            }, transaction);

            transaction.Commit();
            return 1;
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }

    public async Task<int> DeleteUserAsync(int userId)
    {
        using var connection = CreateConnection();
        const string sql = "DELETE FROM Users WHERE Id = @Id";
        return await connection.ExecuteAsync(sql, new { Id = userId });
    }

    public async Task<int> UpdateRefreshTokenAsync(int userId, string? refreshToken, DateTime? expiryTime)
    {
        using var connection = CreateConnection();
        const string sql = "UPDATE Users SET RefreshToken = @RefreshToken, RefreshTokenExpiryTime = @RefreshTokenExpiryTime WHERE Id = @Id";
        return await connection.ExecuteAsync(sql, new { Id = userId, RefreshToken = refreshToken, RefreshTokenExpiryTime = expiryTime });
    }

    public async Task<User?> GetUserByRefreshTokenAsync(string refreshToken)
    {
        using var connection = CreateConnection();
        const string sql = @"
            SELECT u.*, ud.*
            FROM Users u
            LEFT JOIN UserDetails ud ON u.Id = ud.UserId
            WHERE u.RefreshToken = @RefreshToken";
        return await connection.QuerySingleOrDefaultAsync<User>(sql, new { RefreshToken = refreshToken });
    }

    public async Task<User?> GetUserByGoogleIdAsync(string googleId)
    {
        using var connection = CreateConnection();
        const string sql = @"
            SELECT u.*, ud.*
            FROM Users u
            LEFT JOIN UserDetails ud ON u.Id = ud.UserId
            WHERE u.GoogleId = @GoogleId";
        return await connection.QuerySingleOrDefaultAsync<User>(sql, new { GoogleId = googleId });
    }

    public async Task<User?> GetUserByAppleIdAsync(string appleId)
    {
        using var connection = CreateConnection();
        const string sql = @"
            SELECT u.*, ud.*
            FROM Users u
            LEFT JOIN UserDetails ud ON u.Id = ud.UserId
            WHERE u.AppleId = @AppleId";
        return await connection.QuerySingleOrDefaultAsync<User>(sql, new { AppleId = appleId });
    }
    public async Task<int> UpdateGoogleIdAsync(int userId, string googleId)
    {
        using var connection = CreateConnection();
        const string sql = "UPDATE Users SET GoogleId = @GoogleId, AuthProvider = 'Google' WHERE Id = @Id";
        return await connection.ExecuteAsync(sql, new { Id = userId, GoogleId = googleId });
    }

    public async Task<int> UpdateProfileImageAsync(int userId, string imageUrl)
    {
        using var connection = CreateConnection();
        const string sql = "UPDATE Users SET ProfileImageUrl = @ProfileImageUrl WHERE Id = @Id";
        return await connection.ExecuteAsync(sql, new { Id = userId, ProfileImageUrl = imageUrl });
    }
    public async Task<int> UpdatePasswordAsync(int userId, string passwordHash)
    {
        using var connection = CreateConnection();
        const string sql = "UPDATE Users SET PasswordHash = @PasswordHash WHERE Id = @Id";
        return await connection.ExecuteAsync(sql, new { Id = userId, PasswordHash = passwordHash });
    }
}

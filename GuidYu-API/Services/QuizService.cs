using System.Text.Json;
using Dapper;
using GuidYu_API.DTOs;
using Microsoft.Data.SqlClient;

namespace GuidYu_API.Services;

public class QuizService : IQuizService
{
    private readonly IAiService _aiService;
    private readonly string _connectionString;

    public QuizService(IAiService aiService, IConfiguration configuration)
    {
        _aiService = aiService;
        _connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("DefaultConnection string not found.");
    }

    // ─── Generate / Retrieve Quiz ───────────────────────────────────────────────

    public async Task<QuizResponseDto> GetOrGenerateQuizAsync(
        int userId, string lessonId, string lessonContent, string career)
    {
        using var connection = new SqlConnection(_connectionString);

        // 1. Check cache — look for a quiz already saved for this user + lesson
        var cached = await connection.QueryFirstOrDefaultAsync<string>(
            "SELECT QuizJson FROM SavedQuizzes WHERE UserId = @UserId AND LessonId = @LessonId",
            new { UserId = userId, LessonId = lessonId });

        List<QuizQuestionDto> questions;

        if (cached != null)
        {
            // Deserialize from cache (includes correctAnswer — server-side only)
            questions = ParseQuestionsFromJson(cached);
        }
        else
        {
            // 2. Generate via AI
            var rawJson = await _aiService.GenerateQuizAsync(lessonId, lessonContent, career);
            questions = ParseQuestionsFromJson(rawJson);

            // 3. Save FULL quiz (with correctAnswers) to DB — NEVER expose this to frontend
            await connection.ExecuteAsync(
                @"INSERT INTO SavedQuizzes (UserId, LessonId, QuizJson, CreatedAt)
                  VALUES (@UserId, @LessonId, @QuizJson, GETDATE())",
                new
                {
                    UserId = userId,
                    LessonId = lessonId,
                    QuizJson = rawJson
                });
        }

        // 4. Strip correctAnswer before returning to frontend
        return BuildClientResponse(lessonId, questions);
    }

    // ─── Grade Submission ────────────────────────────────────────────────────────

    public async Task<QuizResultDto> SubmitQuizAsync(int userId, QuizSubmitDto submission)
    {
        using var connection = new SqlConnection(_connectionString);

        // 1. Load FULL quiz from server (with correctAnswers) — never trust client
        var savedJson = await connection.QueryFirstOrDefaultAsync<string>(
            "SELECT QuizJson FROM SavedQuizzes WHERE UserId = @UserId AND LessonId = @LessonId",
            new { UserId = userId, LessonId = submission.LessonId });

        if (savedJson == null)
            throw new InvalidOperationException("No quiz found for this lesson. Please generate the quiz first.");

        var questions = ParseQuestionsFromJson(savedJson);

        // 2. Grade answers server-side
        var feedback = new List<QuizAnswerFeedbackDto>();
        int score = 0;

        for (int i = 0; i < questions.Count; i++)
        {
            var submitted = (i < submission.Answers.Count) ? submission.Answers[i] : string.Empty;
            var correct = questions[i].CorrectAnswer;
            bool isCorrect = string.Equals(submitted.Trim(), correct.Trim(), StringComparison.OrdinalIgnoreCase);

            if (isCorrect) score++;

            feedback.Add(new QuizAnswerFeedbackDto
            {
                Index = i,
                Question = questions[i].Question,
                YourAnswer = submitted,
                CorrectAnswer = correct,   // revealed only after submission
                IsCorrect = isCorrect
            });
        }

        int total = questions.Count;
        double scorePercent = total > 0 ? (double)score / total * 100 : 0;
        bool passed = scorePercent >= 70.0;

        // 3. Upsert LessonQuizResults (increment AttemptCount on retry)
        var existing = await connection.QueryFirstOrDefaultAsync<int?>(
            "SELECT Id FROM LessonQuizResults WHERE UserId = @UserId AND LessonId = @LessonId",
            new { UserId = userId, LessonId = submission.LessonId });

        int attemptCount;
        if (existing.HasValue)
        {
            attemptCount = await connection.ExecuteScalarAsync<int>(
                @"UPDATE LessonQuizResults
                  SET Score = @Score, Total = @Total, Passed = @Passed,
                      AttemptCount = AttemptCount + 1, CompletedAt = GETDATE()
                  WHERE UserId = @UserId AND LessonId = @LessonId;
                  SELECT AttemptCount FROM LessonQuizResults WHERE UserId = @UserId AND LessonId = @LessonId;",
                new { UserId = userId, LessonId = submission.LessonId, Score = score, Total = total, Passed = passed });
        }
        else
        {
            await connection.ExecuteAsync(
                @"INSERT INTO LessonQuizResults (UserId, LessonId, Score, Total, Passed, AttemptCount, CompletedAt)
                  VALUES (@UserId, @LessonId, @Score, @Total, @Passed, 1, GETDATE())",
                new { UserId = userId, LessonId = submission.LessonId, Score = score, Total = total, Passed = passed });
            attemptCount = 1;
        }

        // 4. If passed, mark TopicProgress as completed (backend-enforced)
        if (passed && !string.IsNullOrWhiteSpace(submission.TopicTitle))
        {
            var topicId = await connection.QueryFirstOrDefaultAsync<int?>(
                "SELECT Id FROM ModuleTopics WHERE Title = @Title",
                new { Title = submission.TopicTitle });

            if (topicId.HasValue)
            {
                await connection.ExecuteAsync(
                    @"IF EXISTS (SELECT 1 FROM TopicProgress WHERE UserId = @UserId AND TopicId = @TopicId)
                      BEGIN
                          UPDATE TopicProgress
                          SET IsCompleted = 1, LastAccessed = GETDATE()
                          WHERE UserId = @UserId AND TopicId = @TopicId
                      END
                      ELSE
                      BEGIN
                          INSERT INTO TopicProgress (UserId, TopicId, IsCompleted)
                          VALUES (@UserId, @TopicId, 1)
                      END",
                    new { UserId = userId, TopicId = topicId.Value });
            }
        }

        return new QuizResultDto
        {
            Score = score,
            Total = total,
            Passed = passed,
            AttemptCount = attemptCount,
            Feedback = feedback
        };
    }

    // ─── Private Helpers ─────────────────────────────────────────────────────────

    private static List<QuizQuestionDto> ParseQuestionsFromJson(string json)
    {
        using var doc = JsonDocument.Parse(json);

        if (!doc.RootElement.TryGetProperty("questions", out var questionsEl))
            throw new InvalidOperationException("Quiz JSON is missing the 'questions' property.");

        var questions = new List<QuizQuestionDto>();

        foreach (var q in questionsEl.EnumerateArray())
        {
            var options = new List<string>();
            if (q.TryGetProperty("options", out var optionsEl))
                foreach (var opt in optionsEl.EnumerateArray())
                    options.Add(opt.GetString() ?? string.Empty);

            questions.Add(new QuizQuestionDto
            {
                Question = q.TryGetProperty("question", out var qText) ? qText.GetString() ?? "" : "",
                Options = options,
                CorrectAnswer = q.TryGetProperty("correctAnswer", out var ca) ? ca.GetString() ?? "" : ""
            });
        }

        return questions;
    }

    private static QuizResponseDto BuildClientResponse(string lessonId, List<QuizQuestionDto> questions)
    {
        // CorrectAnswer intentionally excluded from client DTO
        var clientQuestions = questions.Select((q, i) => new QuizQuestionClientDto
        {
            Index = i,
            Question = q.Question,
            Options = q.Options
        }).ToList();

        return new QuizResponseDto
        {
            LessonId = lessonId,
            Questions = clientQuestions
        };
    }
}

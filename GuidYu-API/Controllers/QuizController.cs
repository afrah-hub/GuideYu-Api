using System.Security.Claims;
using GuidYu_API.DTOs;
using GuidYu_API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GuidYu_API.Controllers;

[ApiController]
[Route("api/quiz")]
[Authorize]
public class QuizController : ControllerBase
{
    private readonly IQuizService _quizService;

    public QuizController(IQuizService quizService)
    {
        _quizService = quizService;
    }

    // ─── POST /api/quiz/generate ─────────────────────────────────────────────────
    // Accepts lessonContent in JSON body (avoids URL length limits and log exposure).
    // Returns quiz WITHOUT correctAnswer fields — answers are server-side only.
    [HttpPost("generate")]
    public async Task<IActionResult> GenerateQuiz([FromBody] QuizGenerateRequestDto request)
    {
        if (string.IsNullOrWhiteSpace(request.LessonId))
            return BadRequest(new { message = "LessonId is required." });

        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (userIdClaim == null || !int.TryParse(userIdClaim, out var userId))
            return Unauthorized(new { message = "Invalid user token." });

        try
        {
            var quiz = await _quizService.GetOrGenerateQuizAsync(
                userId,
                request.LessonId,
                request.LessonContent ?? string.Empty,
                request.Career ?? string.Empty);

            return Ok(quiz);
        }
        catch (Exception ex)
        {
            // All AI failures (timeout, quota, invalid JSON) return 503 so the
            // frontend can show a friendly "try again" message without crashing.
            Console.WriteLine($"[QuizController] Quiz generation failed for user {userId}, lesson '{request.LessonId}': {ex.Message}");
            return StatusCode(503, new
            {
                message = "We couldn't generate your quiz right now. Please try again shortly.",
                details = ex.Message
            });
        }
    }

    // ─── POST /api/quiz/submit ───────────────────────────────────────────────────
    // Grades answers entirely server-side.
    // Reveals correct answers in the response (only after submission).
    // Marks lesson complete in TopicProgress if score >= 70%.
    [HttpPost("submit")]
    public async Task<IActionResult> SubmitQuiz([FromBody] QuizSubmitDto submission)
    {
        if (string.IsNullOrWhiteSpace(submission.LessonId))
            return BadRequest(new { message = "LessonId is required." });

        if (submission.Answers == null || submission.Answers.Count == 0)
            return BadRequest(new { message = "Answers list cannot be empty." });

        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (userIdClaim == null || !int.TryParse(userIdClaim, out var userId))
            return Unauthorized(new { message = "Invalid user token." });

        try
        {
            var result = await _quizService.SubmitQuizAsync(userId, submission);
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            // Quiz not found — user tried to submit without first generating
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[QuizController] Submit failed for user {userId}, lesson '{submission.LessonId}': {ex.Message}");
            return StatusCode(500, new { message = "Failed to grade quiz. Please try again.", details = ex.Message });
        }
    }
}

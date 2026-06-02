using GuidYu_API.DTOs;

namespace GuidYu_API.Services;

public interface IQuizService
{
    /// <summary>
    /// Returns a cached quiz for the lesson, or generates a new one via AI.
    /// CorrectAnswer is STRIPPED from the response — grading is server-side only.
    /// </summary>
    Task<QuizResponseDto> GetOrGenerateQuizAsync(int userId, string lessonId, string lessonContent, string career);

    /// <summary>
    /// Grades submitted answers entirely server-side.
    /// Updates TopicProgress if score >= 70%. Logs attempt in LessonQuizResults.
    /// </summary>
    Task<QuizResultDto> SubmitQuizAsync(int userId, QuizSubmitDto submission);
}

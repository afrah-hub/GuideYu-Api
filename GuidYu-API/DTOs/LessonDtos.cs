using System.Collections.Generic;

namespace GuidYu_API.DTOs;

public class LessonContentDto
{
    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public List<string> KeyConcepts { get; set; } = new();
    public List<string> Examples { get; set; } = new();
    public string Summary { get; set; } = string.Empty;
    public string Difficulty { get; set; } = "Intermediate";
}

public class AiActionRequestDto
{
    public string ActionType { get; set; } = string.Empty; // "explain", "quiz", "practice"
    public string Context { get; set; } = string.Empty; // Current lesson content or topic
    public string TargetCareer { get; set; } = string.Empty;
}

public class AiActionResponseDto
{
    public string Response { get; set; } = string.Empty;
    public object? Metadata { get; set; } // For structured data like quizzes
}

public class LessonProgressUpdateDto
{
    public int RoadmapId { get; set; }
    public string TopicTitle { get; set; } = string.Empty;
    public bool IsCompleted { get; set; }
}

public class LastStoppedLessonDto
{
    public string Career { get; set; } = string.Empty;
    public string ModuleName { get; set; } = string.Empty;
    public string TopicTitle { get; set; } = string.Empty;
    public string TopicId { get; set; } = string.Empty;
    public int RoadmapId { get; set; }
    public int ModuleId { get; set; }
}

// ─── Quiz DTOs ────────────────────────────────────────────────────────────────

/// <summary>Full question stored server-side in SavedQuizzes.QuizJson — NEVER sent to frontend.</summary>
public class QuizQuestionDto
{
    public string Question { get; set; } = string.Empty;
    public List<string> Options { get; set; } = new();
    public string CorrectAnswer { get; set; } = string.Empty;   // server-side only
}

/// <summary>Safe question sent to frontend — CorrectAnswer intentionally omitted.</summary>
public class QuizQuestionClientDto
{
    public int Index { get; set; }
    public string Question { get; set; } = string.Empty;
    public List<string> Options { get; set; } = new();
}

/// <summary>Response returned from POST /api/quiz/generate — no correct answers exposed.</summary>
public class QuizResponseDto
{
    public string LessonId { get; set; } = string.Empty;
    public List<QuizQuestionClientDto> Questions { get; set; } = new();
}

/// <summary>Request body for POST /api/quiz/generate.</summary>
public class QuizGenerateRequestDto
{
    public string LessonId { get; set; } = string.Empty;
    public string Career { get; set; } = string.Empty;
    public string LessonContent { get; set; } = string.Empty;
}

/// <summary>Request body for POST /api/quiz/submit. Answers list aligns by index with Questions list.</summary>
public class QuizSubmitDto
{
    public string LessonId { get; set; } = string.Empty;
    public int RoadmapId { get; set; }
    public string TopicTitle { get; set; } = string.Empty;
    public List<string> Answers { get; set; } = new();   // one answer per question, by index
}

/// <summary>Result returned after grading — reveals correct answers only post-submission.</summary>
public class QuizResultDto
{
    public int Score { get; set; }          // number of correct answers
    public int Total { get; set; }          // total questions
    public bool Passed { get; set; }        // Score/Total >= 0.70
    public int AttemptCount { get; set; }
    public List<QuizAnswerFeedbackDto> Feedback { get; set; } = new();
}

/// <summary>Per-question feedback shown on result screen.</summary>
public class QuizAnswerFeedbackDto
{
    public int Index { get; set; }
    public string Question { get; set; } = string.Empty;
    public string YourAnswer { get; set; } = string.Empty;
    public string CorrectAnswer { get; set; } = string.Empty;   // revealed after submit
    public bool IsCorrect { get; set; }
}

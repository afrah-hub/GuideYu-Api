namespace GuidYu_API.Services;

public interface IAiService
{
    Task<string> GetCareerRecommendationsAsync(string skills, string interests, string education, string targetCareer);
    Task<string> GetLearningPlanAsync(string targetCareer, string currentSkills);
    Task<string> GenerateCareerPathOverviewAsync(string currentRole, string targetCareer, string currentSkills);
    Task<string> GetStudyMaterialsAsync(string targetCareer, string currentSkills, string education);
    Task<string> GetSyllabusAsync(string moduleName, string targetCareer);
    Task<string> GenerateLessonContentAsync(string topic, string targetCareer, string difficulty);
    Task<string> ProcessAiActionAsync(string actionType, string context, string targetCareer);
    Task<string> GenerateQuizAsync(string topic, string lessonContent, string career);

}

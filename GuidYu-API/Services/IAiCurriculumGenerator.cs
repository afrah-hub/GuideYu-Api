namespace GuidYu_API.Services
{
    public interface IAiCurriculumGenerator
    {
        /// <summary>
        /// Generates a career-specific curriculum JSON based on selected career and learning stage.
        /// </summary>
        /// <param name="career">The target career name.</param>
        /// <param name="stage">The learning stage (e.g., Beginner, Intermediate, Advanced).</param>
        /// <returns>Raw JSON string representing the curriculum.</returns>
        Task<string> GenerateCurriculumAsync(string career, string stage);
    }
}

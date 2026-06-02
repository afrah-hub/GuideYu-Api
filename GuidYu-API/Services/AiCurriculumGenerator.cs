using System.Threading.Tasks;
using GuidYu_API.Services;

namespace GuidYu_API.Services
{
    public class AiCurriculumGenerator : IAiCurriculumGenerator
    {
        private readonly IAiService _aiService;

        public AiCurriculumGenerator(IAiService aiService)
        {
            _aiService = aiService;
        }

        public async Task<string> GenerateCurriculumAsync(string career, string stage)
        {
            // Construct a minimal prompt using existing AI service method.
            // The IAiService currently provides GenerateCareerPathOverviewAsync which returns a JSON overview.
            // We repurpose it by passing the stage as the current role and the career as the target career.
            var json = await _aiService.GenerateCareerPathOverviewAsync(stage, career, string.Empty);
            return json ?? string.Empty;
        }
    }
}

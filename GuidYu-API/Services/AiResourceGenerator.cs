using System.Threading.Tasks;

namespace GuidYu_API.Services
{
    public interface IAiResourceGenerator
    {
        Task<string> GenerateStudyMaterialsAsync(string targetCareer, string currentSkills, string education);
        Task<string> GenerateSyllabusAsync(string moduleName, string targetCareer);
    }

    public class AiResourceGenerator : IAiResourceGenerator
    {
        private readonly IAiService _aiService;

        public AiResourceGenerator(IAiService aiService)
        {
            _aiService = aiService;
        }

        public async Task<string> GenerateStudyMaterialsAsync(string targetCareer, string currentSkills, string education)
        {
            return await _aiService.GetStudyMaterialsAsync(targetCareer, currentSkills, education);
        }

        public async Task<string> GenerateSyllabusAsync(string moduleName, string targetCareer)
        {
            return await _aiService.GetSyllabusAsync(moduleName, targetCareer);
        }
    }
}

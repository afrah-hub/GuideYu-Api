using System.Threading.Tasks;
using GuidYu_API.DTOs;
using System.Text.Json;

namespace GuidYu_API.Services
{
    public interface IAiLessonGenerator
    {
        Task<LessonContentDto?> GenerateLessonAsync(string topic, string career, string difficulty);
    }

    public class AiLessonGenerator : IAiLessonGenerator
    {
        private readonly IAiService _aiService;
        public AiLessonGenerator(IAiService aiService)
        {
            _aiService = aiService;
        }

        public async Task<LessonContentDto?> GenerateLessonAsync(string topic, string career, string difficulty)
        {
            var json = await _aiService.GenerateLessonContentAsync(topic, career, difficulty);
            var lesson = JsonSerializer.Deserialize<LessonContentDto>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            return lesson;
        }
    }
}

namespace GuidYu_API.DTOs;

public class AIRecommendationResponse
{
    public List<AICareerSuggestion> Careers { get; set; } = new();
}

public class AICareerSuggestion
{
    public string Name { get; set; } = string.Empty;
    public int MatchScore { get; set; }
    public string Reason { get; set; } = string.Empty;
}

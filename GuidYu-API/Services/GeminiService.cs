using System.Text;
using System.Text.Json;
using System.Net.Http.Headers;

namespace GuidYu_API.Services;

public class GeminiService : IAiService
{
    private readonly HttpClient _httpClient;
    private readonly string _apiKey;

    public GeminiService(HttpClient httpClient, IConfiguration configuration)
    {
        _httpClient = httpClient;
        _apiKey = configuration["Gemini:ApiKey"] ?? throw new ArgumentNullException("Gemini ApiKey is missing in configuration.");
    }

    private async Task<string> CallGeminiApiAsync(string prompt)
    {
        int maxRetries = 3;
        int delayMs = 2000;

        // Check if JSON response is requested
        bool requestJson = prompt.Contains("JSON", StringComparison.OrdinalIgnoreCase) || 
                           prompt.Contains("json", StringComparison.OrdinalIgnoreCase);

        for (int i = 0; i < maxRetries; i++)
        {
            try
            {
                var requestBody = new Dictionary<string, object>
                {
                    { "model", "gpt-4o-mini" },
                    { "messages", new[]
                        {
                            new { role = "system", content = "You are a professional career advisor." },
                            new { role = "user", content = prompt }
                        }
                    },
                    { "temperature", 0.7 }
                };

                if (requestJson)
                {
                    requestBody.Add("response_format", new { type = "json_object" });
                }

                var jsonPayload = JsonSerializer.Serialize(requestBody);
                var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

                var request = new HttpRequestMessage(HttpMethod.Post, "https://api.vectorengine.ai/v1/chat/completions");
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);
                request.Content = content;

                var response = await _httpClient.SendAsync(request);

                if (response.IsSuccessStatusCode)
                {
                    var jsonResponse = await response.Content.ReadAsStringAsync();
                    try 
                    {
                        using var doc = JsonDocument.Parse(jsonResponse);
                        if (doc.RootElement.TryGetProperty("choices", out var choices) && choices.GetArrayLength() > 0)
                        {
                            var choice = choices[0];
                            if (choice.TryGetProperty("message", out var messageObj) && 
                                messageObj.TryGetProperty("content", out var contentProp))
                            {
                                var text = contentProp.GetString();
                                return text ?? string.Empty;
                            }
                        }
                        throw new Exception("VectorEngine response format invalid.");
                    }
                    catch (JsonException ex)
                    {
                        throw new Exception("Failed to parse VectorEngine JSON response: " + ex.Message);
                    }
                }

                if ((int)response.StatusCode == 429 && i < maxRetries - 1)
                {
                    await Task.Delay(delayMs * (i + 1));
                    continue;
                }

                var errorContent = await response.Content.ReadAsStringAsync();
                Console.WriteLine($"VectorEngine API Error ({response.StatusCode}): {errorContent}");
                throw new Exception($"VectorEngine Service returned {response.StatusCode}. Details: {errorContent}");
            }
            catch (TaskCanceledException)
            {
                Console.WriteLine("VectorEngine API Timeout exceeded.");
                if (i < maxRetries - 1)
                {
                    await Task.Delay(delayMs * (i + 1));
                    continue;
                }
                throw new Exception("VectorEngine Service timed out.");
            }
            catch (HttpRequestException ex)
            {
                Console.WriteLine($"VectorEngine API Request Error: {ex.Message}");
                if (i < maxRetries - 1)
                {
                    await Task.Delay(delayMs * (i + 1));
                    continue;
                }
                throw;
            }
        }

        throw new Exception("VectorEngine Service encountered an unexpected termination.");
    }

    public async Task<string> GetCareerRecommendationsAsync(string skills, string interests, string education, string targetCareer)
    {
        var prompt = $@"
Suggest 6 to 8 suitable careers for a user based on their data:
Target Career Goal: {targetCareer}
Skills: {skills}
Interests: {interests}
Education: {education}

Instructions:
* Return ONLY JSON
* Include: name, matchScore (0-100), reason
* Keep reason short (1 line)
* Sort by highest match score

Output format:
{{
  ""careers"": [
    {{
      ""name"": ""Frontend Developer"",
      ""matchScore"": 80,
      ""reason"": ""Matches your JavaScript and UI skills""
    }}
  ]
}}";

        return await CallGeminiApiAsync(prompt);
    }

    public async Task<string> GetLearningPlanAsync(string targetCareer, string currentSkills)
    {
        var prompt = $@"
Generate a concise learning plan for: {targetCareer}.
Current skills: {currentSkills}.

Instructions:
* Return ONLY JSON.
* Identify 3 key skill gaps.
* For each skill, provide:
  - name: Skill name
  - priority: ""High"", ""Medium"", or ""Low""
  - resources: Array of 2 resources (title, platform)
  - projects: Array of 1 practical project.
* Overall progress (0-100).

Output format:
{{
  ""targetCareer"": ""{targetCareer}"",
  ""overallProgress"": 35,
  ""skills"": [
    {{
      ""name"": ""React.js"",
      ""priority"": ""High"",
      ""status"": ""Not Started"",
      ""resources"": [
        {{ ""title"": ""React Basics"", ""platform"": ""YouTube"" }}
      ],
      ""projects"": [""Build a Portfolio""]
    }}
  ]
}}";

        return await CallGeminiApiAsync(prompt);
    }

    public async Task<string> GenerateCareerPathOverviewAsync(string currentRole, string targetCareer, string currentSkills)
    {
        var prompt = $@"
Generate a career roadmap from '{currentRole}' to '{targetCareer}'.
Current skills: {currentSkills}.

Instructions:
* Return ONLY JSON.
* Journey: 3 steps (Current, Mid-level, Target).
* For EACH skill in the 'Skills' array, include 2-3 'Lessons'.
* Insights: 2 actionable points.

Output format:
{{
  ""summary"": {{
    ""currentRole"": ""{currentRole}"",
    ""targetRole"": ""{targetCareer}"",
    ""matchPercentage"": 75,
    ""estimatedTime"": ""1-2 years""
  }},
  ""journey"": [
    {{ 
      ""roleName"": ""{currentRole}"", 
      ""status"": ""Current"", 
      ""isCurrent"": true, 
      ""order"": 1,
      ""skills"": [
        {{ 
          ""name"": ""Foundations"", 
          ""category"": ""Completed"", 
          ""progress"": 100, 
          ""lessons"": [
            {{ ""title"": ""Basics"", ""duration"": ""15m"", ""isCompleted"": true }}
          ]
        }}
      ]
    }}
  ],
  ""skills"": [
    {{ 
      ""name"": ""Core Skill"", 
      ""category"": ""InProgress"", 
      ""progress"": 40, 
      ""lessons"": [
        {{ ""title"": ""Lesson 1"", ""duration"": ""30m"", ""isCompleted"": false }}
      ]
    }}
  ],
  ""insights"": [
    {{ ""text"": ""Focus on Skill X"", ""impactValue"": ""15%"" }}
  ]
}}";

        return await CallGeminiApiAsync(prompt);
    }

    public async Task<string> GetStudyMaterialsAsync(string targetCareer, string currentSkills, string education)
    {
        var prompt = $@"
Generate study materials for: {targetCareer}.
Skills: {currentSkills}.

Instructions:
* Return ONLY JSON.
* 2-3 categories.
* Each category: 2-3 items.

Output format:
{{
  ""targetCareer"": ""{targetCareer}"",
  ""categories"": [
    {{
      ""categoryName"": ""Fundamentals"",
      ""materials"": [
        {{
          ""title"": ""{targetCareer} Guide"",
          ""type"": ""Video"",
          ""description"": ""Brief intro."",
          ""link"": ""https://youtube.com/results?search_query={targetCareer}"",
          ""estimatedTime"": ""30 mins""
        }}
      ]
    }}
  ]
}}";

        return await CallGeminiApiAsync(prompt);
    }

    public async Task<string> GetSyllabusAsync(string moduleName, string targetCareer)
    {
        var prompt = $@"
Generate a concise, professional syllabus for the module '{moduleName}' as part of a learning journey for '{targetCareer}'.

Instructions:
* Return ONLY JSON.
* Break the module into 4 to 6 logical topics.
* For each topic, provide:
  - title: Name of the topic.
  - description: 1 clear sentence summary.
  - keyTakeaways: Array of 2 to 3 bullet points.
  - estimatedTime: (e.g., '45 mins', '2 hours').
  - difficulty: 'Beginner', 'Intermediate', or 'Advanced'.

Output format:
{{
  ""moduleName"": ""{moduleName}"",
  ""targetCareer"": ""{targetCareer}"",
  ""topics"": [
    {{
      ""title"": ""Foundations of {moduleName}"",
      ""description"": ""Learn the fundamental concepts and core architecture."",
      ""keyTakeaways"": [""Concept A"", ""Core Principle B""],
      ""estimatedTime"": ""1 hour"",
      ""difficulty"": ""Beginner""
    }}
  ]
}}";

        return await CallGeminiApiAsync(prompt);
    }

    public async Task<string> GenerateLessonContentAsync(string topic, string targetCareer, string difficulty)
    {
        var prompt = $@"
Generate an extremely comprehensive, highly detailed, and professional academic course lesson for the topic: '{topic}'.
Target Career: {targetCareer}
Difficulty Level: {difficulty}

Instructions:
* Return ONLY JSON matching the requested structure. Do not wrap in markdown json blocks.
* The lesson content must be extremely thorough, beginner-friendly, and feel like a premium course lesson.
* The 'content' field must contain the full study guide formatted in beautiful, readable markdown. It must be structured with the following 14 sections using '##' headers exactly:

  ## Executive Overview
  [A detailed, high-level summary of the topic and its relevance to a {targetCareer}]

  ## Why This Topic Matters
  [A thorough explanation of the real-world value of this topic, the problems it solves, and why every professional in this field must master it]

  ## Core Concepts
  [Explain all fundamental terms, architectures, principles, and theories behind the topic. Use structured lists or tables where appropriate]

  ## Step-by-Step Explanation
  [A clear, logical walk-through explaining how this topic/system works or is implemented from start to finish]

  ## Real-World Examples
  [Provide 2-3 specific, real-world examples of this topic in action, highlighting how it applies to practical problems]

  ## Industry Use Cases
  [Explain how top tech companies or industry sectors apply this topic to achieve scale, efficiency, or reliability]

  ## Tools & Technologies
  [List and compare the popular tools, frameworks, libraries, or protocols associated with this topic]

  ## Best Practices
  [A comprehensive list of engineering and architectural best practices, patterns, or guidelines]

  ## Common Mistakes
  [Highlight frequent errors, anti-patterns, or misconceptions developers/engineers make and how to avoid them]

  ## Security/Performance Considerations
  [Analyze the security risks, performance bottlenecks, optimization strategies, and scalability considerations]

  ## Summary
  [A high-impact summary of the main themes of the lesson]

  ## Key Takeaways
  [Bullet points summarizing the essential facts and learnings to remember]

  ## Interview Questions
  [List 3-5 typical technical interview questions about this topic along with brief, high-scoring suggested answers]

  ## Mini Practice Tasks
  [Provide 1-2 small, hands-on exercise tasks or challenges for the student to build or run to reinforce learning]

* The JSON response must look exactly like this:
{{
  ""title"": ""{topic}"",
  ""content"": ""## Executive Overview\n\n[Overview content...]\n\n## Why This Topic Matters\n\n[Why it matters...]\n\n## Core Concepts\n\n[Core concepts...]\n\n## Step-by-Step Explanation\n\n[Step-by-step...]\n\n## Real-World Examples\n\n[Examples...]\n\n## Industry Use Cases\n\n[Use cases...]\n\n## Tools & Technologies\n\n[Tools...]\n\n## Best Practices\n\n[Best practices...]\n\n## Common Mistakes\n\n[Mistakes...]\n\n## Security/Performance Considerations\n\n[Security/perf...]\n\n## Summary\n\n[Summary...]\n\n## Key Takeaways\n\n[Takeaways...]\n\n## Interview Questions\n\n[Interview questions...]\n\n## Mini Practice Tasks\n\n[Practice tasks...]"",
  ""keyConcepts"": [""Key Concept 1"", ""Key Concept 2"", ""Key Concept 3""],
  ""examples"": [""Real-world example 1"", ""Real-world example 2""],
  ""summary"": ""Concise summary statement."",
  ""difficulty"": ""{difficulty}""
}}";

        return await CallGeminiApiAsync(prompt);
    }

    public async Task<string> ProcessAiActionAsync(string actionType, string context, string targetCareer)
    {
        string systemMessage = actionType.ToLower() switch
        {
            "explain" => "Explain the following topic or content in much simpler terms for a beginner, while keeping it relevant to a " + targetCareer + " role.",
            "quiz" => "Generate a 3-question multiple choice quiz based on the following content. Return ONLY JSON with 'questions' array containing 'text', 'options', and 'correctAnswer'.",
            "practice" => "Generate a practical, hands-on task or mini-project based on the following content to help a " + targetCareer + " student practice their skills.",
            _ => "Provide professional assistance for the following content."
        };

        var prompt = $@"
Action: {actionType}
Target Career: {targetCareer}
Content: {context}

Instructions:
* Respond based on the action type.
* If it's a quiz, return JSON. Otherwise, return professional markdown text.";

        return await CallGeminiApiAsync(prompt);
    }

    public async Task<string> GenerateQuizAsync(string topic, string lessonContent, string career)
    {
        // Truncate lessonContent to avoid exceeding token limits (~4000 chars is plenty for context)
        var contentPreview = lessonContent.Length > 4000
            ? lessonContent.Substring(0, 4000) + "..."
            : lessonContent;

        var prompt = $@"
You are an expert educator creating a professional quiz for a career learning platform.

Topic: {topic}
Career Track: {career}
Lesson Content (for context):
{contentPreview}

Instructions:
* Generate exactly 7 multiple-choice questions that test understanding of the topic above.
* Each question must have exactly 4 answer options labeled as the full option text (not A/B/C/D labels).
* The correctAnswer field must exactly match one of the 4 options strings.
* Questions should range from basic recall to applied understanding.
* Return ONLY valid JSON — no markdown, no explanation, no code fences.

Required JSON format:
{{
  ""questions"": [
    {{
      ""question"": ""What is the primary purpose of X?"",
      ""options"": [""Option A text"", ""Option B text"", ""Option C text"", ""Option D text""],
      ""correctAnswer"": ""Option A text""
    }}
  ]
}}";

        try
        {
            var result = await CallGeminiApiAsync(prompt);

            // Validate the returned JSON has the expected shape before returning
            using var doc = JsonDocument.Parse(result);
            if (!doc.RootElement.TryGetProperty("questions", out var questionsEl) || questionsEl.GetArrayLength() == 0)
            {
                throw new InvalidOperationException("AI returned quiz JSON without a valid 'questions' array.");
            }

            return result;
        }
        catch (TaskCanceledException ex)
        {
            Console.WriteLine($"[QuizGen] Timeout generating quiz for '{topic}': {ex.Message}");
            throw new Exception("Quiz generation timed out. Please try again.");
        }
        catch (HttpRequestException ex)
        {
            Console.WriteLine($"[QuizGen] Network error generating quiz for '{topic}': {ex.Message}");
            throw new Exception("Network error during quiz generation. Please check connectivity.");
        }
        catch (JsonException ex)
        {
            Console.WriteLine($"[QuizGen] Invalid JSON returned for quiz '{topic}': {ex.Message}");
            throw new Exception("AI returned an invalid quiz format. Please retry.");
        }
        catch (Exception ex) when (!ex.Message.StartsWith("Quiz generation") && !ex.Message.StartsWith("Network") && !ex.Message.StartsWith("AI returned"))
        {
            Console.WriteLine($"[QuizGen] Unexpected error for '{topic}': {ex.Message}");
            throw new Exception("An unexpected error occurred during quiz generation. Please try again shortly.");
        }
    }
}

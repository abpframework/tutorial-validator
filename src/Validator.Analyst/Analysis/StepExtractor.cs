using System.Text.Json;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Validator.Analyst.Analysis.PromptTemplates;
using Validator.Analyst.Scraping.Models;
using Validator.Core;
using Validator.Core.Models;
using Validator.Core.Models.Steps;

namespace Validator.Analyst.Analysis;

/// <summary>
/// Extracts tutorial steps from markdown content using AI.
/// </summary>
public class StepExtractor
{
    private readonly Kernel _kernel;
    private readonly IChatCompletionService _chatService;

    /// <summary>
    /// Creates a new StepExtractor with the provided Semantic Kernel.
    /// </summary>
    public StepExtractor(Kernel kernel)
    {
        _kernel = kernel;
        _chatService = kernel.GetRequiredService<IChatCompletionService>();
    }

    /// <summary>
    /// Extracts steps from a single tutorial page.
    /// </summary>
    /// <param name="page">The tutorial page to analyze</param>
    /// <param name="configuration">Tutorial configuration (UI, DB)</param>
    /// <param name="totalPages">Total number of pages in the tutorial</param>
    /// <param name="startingStepId">The step ID to start numbering from</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of extracted steps</returns>
    public async Task<List<TutorialStep>> ExtractStepsFromPageAsync(
        TutorialPage page,
        TutorialConfiguration configuration,
        int totalPages,
        int startingStepId,
        CancellationToken cancellationToken = default)
    {
        var chatHistory = new ChatHistory();
        
        // Add system prompt
        chatHistory.AddSystemMessage(SystemPrompts.TutorialAnalyzer + "\n\n" + SystemPrompts.StepSchemaReference);
        
        // Add user prompt for this page
        var userPrompt = UserPrompts.AnalyzePage(
            page.Title,
            page.PageIndex,
            totalPages,
            configuration.Ui,
            configuration.Database,
            page.Content,
            startingStepId);
        
        chatHistory.AddUserMessage(userPrompt);

        // Get AI response
        var response = await _chatService.GetChatMessageContentAsync(
            chatHistory,
            cancellationToken: cancellationToken);

        var responseText = response.Content ?? "[]";
        
        // Parse the response as JSON
        return ParseStepsFromResponse(responseText);
    }

    /// <summary>
    /// Extracts steps from all pages of a tutorial.
    /// </summary>
    /// <param name="tutorial">The scraped tutorial</param>
    /// <param name="configuration">Tutorial configuration</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>All extracted steps in order</returns>
    public async Task<List<TutorialStep>> ExtractAllStepsAsync(
        ScrapedTutorial tutorial,
        TutorialConfiguration configuration,
        CancellationToken cancellationToken = default)
    {
        var allSteps = new List<TutorialStep>();
        var currentStepId = 1;

        foreach (var page in tutorial.Pages)
        {
            Console.WriteLine($"Analyzing page {page.PageIndex + 1}/{tutorial.TotalPages}: {page.Title}");
            
            var pageSteps = await ExtractStepsFromPageAsync(
                page,
                configuration,
                tutorial.TotalPages,
                currentStepId,
                cancellationToken);

            if (pageSteps.Count > 0)
            {
                allSteps.AddRange(pageSteps);
                currentStepId = allSteps.Max(s => s.StepId) + 1;
            }
        }

        return allSteps;
    }

    /// <summary>
    /// Parses steps from the AI response text.
    /// </summary>
    private static List<TutorialStep> ParseStepsFromResponse(string responseText)
    {
        // Extract JSON from the response (may be wrapped in markdown code blocks)
        var json = ExtractJson(responseText);
        
        if (string.IsNullOrWhiteSpace(json))
        {
            return [];
        }

        try
        {
            var steps = JsonSerializer.Deserialize<List<TutorialStep>>(
                json, 
                JsonSerializerOptionsProvider.Default);
            
            return steps ?? [];
        }
        catch (JsonException ex)
        {
            Console.WriteLine($"Warning: Failed to parse full steps JSON: {ex.Message}");
            
            // Try to parse individual steps to salvage what we can
            return ParseStepsIndividually(json);
        }
    }

    /// <summary>
    /// Attempts to parse steps individually when full array parsing fails.
    /// </summary>
    private static List<TutorialStep> ParseStepsIndividually(string json)
    {
        var steps = new List<TutorialStep>();
        
        try
        {
            // Parse as JsonDocument to iterate over elements
            using var doc = JsonDocument.Parse(json);
            
            if (doc.RootElement.ValueKind != JsonValueKind.Array)
            {
                Console.WriteLine("Warning: Response is not a JSON array");
                return steps;
            }

            var successCount = 0;
            var failCount = 0;
            
            foreach (var element in doc.RootElement.EnumerateArray())
            {
                try
                {
                    var stepJson = element.GetRawText();
                    var step = JsonSerializer.Deserialize<TutorialStep>(
                        stepJson, 
                        JsonSerializerOptionsProvider.Default);
                    
                    if (step != null)
                    {
                        steps.Add(step);
                        successCount++;
                    }
                }
                catch (JsonException)
                {
                    failCount++;
                    // Skip this step but continue with others
                }
            }
            
            if (failCount > 0)
            {
                Console.WriteLine($"  Recovered {successCount} steps, {failCount} steps failed to parse");
            }
        }
        catch (JsonException ex)
        {
            Console.WriteLine($"Warning: Could not parse JSON at all: {ex.Message}");
        }
        
        return steps;
    }

    /// <summary>
    /// Extracts JSON content from a response that may include markdown code blocks.
    /// </summary>
    private static string ExtractJson(string text)
    {
        // Try to find JSON array in the text
        var trimmed = text.Trim();
        
        // Check if it's a markdown code block
        if (trimmed.StartsWith("```"))
        {
            var lines = trimmed.Split('\n');
            var startIndex = 1; // Skip the opening ```json or ```
            var endIndex = lines.Length - 1;
            
            // Find the closing ```
            for (int i = lines.Length - 1; i >= 0; i--)
            {
                if (lines[i].Trim() == "```")
                {
                    endIndex = i;
                    break;
                }
            }
            
            var jsonLines = lines.Skip(startIndex).Take(endIndex - startIndex);
            trimmed = string.Join('\n', jsonLines);
        }
        else
        {
            // Try to find a JSON array directly
            var arrayStart = trimmed.IndexOf('[');
            var arrayEnd = trimmed.LastIndexOf(']');
            
            if (arrayStart >= 0 && arrayEnd > arrayStart)
            {
                trimmed = trimmed[arrayStart..(arrayEnd + 1)];
            }
        }
        
        // Sanitize the JSON to fix common AI output issues
        return SanitizeJson(trimmed);
    }

    /// <summary>
    /// Sanitizes JSON output to fix common AI-generated issues.
    /// </summary>
    private static string SanitizeJson(string json)
    {
        // Fix: AI sometimes outputs actual newlines in string values instead of \n
        // This regex finds string values and normalizes their content
        // We need to be careful not to break valid JSON structure
        
        var result = new System.Text.StringBuilder();
        var inString = false;
        var escapeNext = false;
        
        for (int i = 0; i < json.Length; i++)
        {
            var c = json[i];
            
            if (escapeNext)
            {
                result.Append(c);
                escapeNext = false;
                continue;
            }
            
            if (c == '\\')
            {
                result.Append(c);
                escapeNext = true;
                continue;
            }
            
            if (c == '"')
            {
                inString = !inString;
                result.Append(c);
                continue;
            }
            
            if (inString)
            {
                // Replace actual newlines/tabs in strings with escape sequences
                switch (c)
                {
                    case '\n':
                        result.Append("\\n");
                        break;
                    case '\r':
                        result.Append("\\r");
                        break;
                    case '\t':
                        result.Append("\\t");
                        break;
                    default:
                        result.Append(c);
                        break;
                }
            }
            else
            {
                result.Append(c);
            }
        }
        
        return result.ToString();
    }
}

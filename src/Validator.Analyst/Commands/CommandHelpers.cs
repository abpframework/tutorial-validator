using System.Text.Json;
using Validator.Analyst.Analysis;
using Validator.Analyst.Scraping.Models;
using Validator.Core;

namespace Validator.Analyst.Commands;

/// <summary>
/// Shared helper methods for CLI command handlers.
/// </summary>
internal static class CommandHelpers
{
    /// <summary>
    /// Loads and validates AI configuration, exiting the process on failure.
    /// </summary>
    /// <param name="configPath">Optional path to a configuration file.</param>
    /// <returns>A validated AI configuration.</returns>
    internal static AIConfiguration LoadAndValidateAIConfig(string? configPath)
    {
        Console.WriteLine("Loading AI configuration...");
        var aiConfig = KernelFactory.LoadConfiguration(configPath);

        try
        {
            KernelFactory.ValidateConfiguration(aiConfig);
        }
        catch (InvalidOperationException ex)
        {
            Console.Error.WriteLine($"Error: {ex.Message}");
            Console.Error.WriteLine();
            Console.Error.WriteLine("Please set the required environment variables or create an appsettings.json file.");
            Environment.Exit(1);
        }

        return aiConfig;
    }

    /// <summary>
    /// Saves scraped tutorial content to disk as JSON and per-page markdown files.
    /// </summary>
    /// <param name="tutorial">The scraped tutorial to save.</param>
    /// <param name="outputDir">Directory to write files to.</param>
    internal static async Task SaveScrapedContentAsync(ScrapedTutorial tutorial, string outputDir)
    {
        // Output JSON
        var jsonPath = Path.Combine(outputDir, "tutorial.json");
        var json = JsonSerializer.Serialize(tutorial, JsonSerializerOptionsProvider.Default);
        await File.WriteAllTextAsync(jsonPath, json);
        Console.WriteLine($"JSON output written to: {jsonPath}");

        // Output markdown files for each page
        foreach (var page in tutorial.Pages)
        {
            var mdFileName = $"page-{page.PageIndex + 1}.md";
            var mdPath = Path.Combine(outputDir, mdFileName);

            var mdContent = $"# {page.Title}\n\n";
            mdContent += $"> Source: {page.Url}\n\n";
            mdContent += page.Content;

            await File.WriteAllTextAsync(mdPath, mdContent);
            Console.WriteLine($"Markdown written to: {mdPath}");
        }
    }
}

using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Validator.Core;
using Validator.Core.Models;
using Validator.Orchestrator.Models;

namespace Validator.Orchestrator.Runners;

/// <summary>
/// Runs the Validator.Analyst to generate a test plan from a tutorial URL.
/// </summary>
public class AnalystRunner
{
    private readonly IConfiguration _configuration;

    public AnalystRunner(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    /// <summary>
    /// Run the analyst to scrape and analyze a tutorial.
    /// </summary>
    /// <param name="tutorialUrl">URL of the tutorial to analyze.</param>
    /// <param name="outputPath">Directory to output the test plan.</param>
    /// <returns>Result containing the path to the generated test plan.</returns>
    public async Task<AnalystResult> RunAsync(string tutorialUrl, string outputPath)
    {
        var result = new AnalystResult();
        var logPath = Path.Combine(outputPath, "logs", "analyst.log");

        try
        {
            var analystPath = FindAnalystPath();
            var scrapedPath = Path.Combine(outputPath, "scraped");
            var testPlanPath = Path.Combine(outputPath, "testplan.json");

            Console.WriteLine($"Running analyst for: {tutorialUrl}");

            // Build arguments for the 'full' command (scrape + analyze)
            var arguments = $"full --url \"{tutorialUrl}\" --output \"{scrapedPath}\"";

            // Add config path if available
            var configPath = GetConfigPath();
            if (!string.IsNullOrEmpty(configPath))
            {
                arguments += $" --config \"{configPath}\"";
            }

            // Run the analyst process
            var processResult = await ProcessHelper.RunAsync(
                "dotnet",
                $"run --project \"{analystPath}\" -- {arguments}",
                Path.GetDirectoryName(analystPath)!,
                TimeSpan.FromMinutes(30)
            );

            // Save log
            Directory.CreateDirectory(Path.GetDirectoryName(logPath)!);
            await File.WriteAllTextAsync(logPath, $"=== Analyst Output ===\n{processResult.Output}\n\n=== Analyst Errors ===\n{processResult.Error}");
            result.LogPath = logPath;

            if (!processResult.Success)
            {
                result.Success = false;
                result.Error = $"Analyst process failed with exit code {processResult.ExitCode}: {processResult.Error}";
                return result;
            }

            // Find the generated testplan.json
            var generatedTestPlan = FindGeneratedTestPlan(scrapedPath);
            if (generatedTestPlan == null)
            {
                result.Success = false;
                result.Error = "Analyst completed but testplan.json was not found";
                return result;
            }

            // Move testplan to output root
            File.Copy(generatedTestPlan, testPlanPath, overwrite: true);

            // Extract tutorial name from testplan
            try
            {
                var json = await File.ReadAllTextAsync(testPlanPath);
                var testPlan = JsonSerializer.Deserialize<TestPlan>(json, JsonSerializerOptionsProvider.Default);
                result.TutorialName = testPlan?.TutorialName;
            }
            catch
            {
                // Ignore - tutorial name is optional
            }

            result.Success = true;
            result.TestPlanPath = testPlanPath;
            return result;
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.Error = ex.Message;
            return result;
        }
    }

    private static string FindAnalystPath()
    {
        return ProjectPathResolver.FindProjectFile("Validator.Analyst");
    }

    private string? GetConfigPath()
    {
        // First check if there's an explicit config path in configuration
        var configPath = _configuration.GetValue<string>("AI:ConfigPath");
        if (!string.IsNullOrEmpty(configPath) && File.Exists(configPath))
        {
            Console.WriteLine($"Using config from configuration: {configPath}");
            return Path.GetFullPath(configPath);
        }

        // Use the Orchestrator's own appsettings.json as the single source of truth.
        // Check current working directory first (covers `dotnet run` from the project dir),
        // then the application base directory (covers published/deployed scenarios).
        var locations = new[]
        {
            Path.Combine(Directory.GetCurrentDirectory(), "appsettings.json"),
            Path.Combine(AppContext.BaseDirectory, "appsettings.json"),
        };

        foreach (var location in locations)
        {
            if (File.Exists(location))
            {
                Console.WriteLine($"Using AI config: {Path.GetFullPath(location)}");
                return Path.GetFullPath(location);
            }
        }

        Console.WriteLine("Warning: No appsettings.json found. Falling back to environment variables.");
        return null;
    }

    private string? FindGeneratedTestPlan(string scrapedPath)
    {
        // Check for testplan.json in various locations
        var possibleLocations = new[]
        {
            Path.Combine(scrapedPath, "testplan.json"),
            Path.Combine(Path.GetDirectoryName(scrapedPath)!, "testplan.json"),
            Path.Combine(scrapedPath, "..", "testplan.json")
        };

        foreach (var location in possibleLocations)
        {
            if (File.Exists(location))
            {
                return Path.GetFullPath(location);
            }
        }

        // Search recursively in the scraped directory
        try
        {
            var files = Directory.GetFiles(scrapedPath, "testplan.json", SearchOption.AllDirectories);
            if (files.Length > 0)
            {
                return files[0];
            }
        }
        catch
        {
            // Ignore search errors
        }

        return null;
    }
}

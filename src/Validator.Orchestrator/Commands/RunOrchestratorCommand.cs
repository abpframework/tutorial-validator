using System.Text.Json;
using Validator.Core;
using Validator.Core.Models.Enums;
using Validator.Orchestrator.Runners;

namespace Validator.Orchestrator.Commands;

/// <summary>
/// Handles the "run" CLI command.
/// Runs the full validation pipeline (analyst + executor).
/// </summary>
internal static class RunOrchestratorCommand
{
    internal static async Task<int> RunAsync(string[] args)
    {
        var options = CommandHelpers.ParseOptions(args);

        if (string.IsNullOrEmpty(options.TutorialUrl) && !options.SkipAnalyst)
        {
            Console.WriteLine("Error: --url is required unless --skip-analyst is specified.");
            return 1;
        }

        if (options.SkipAnalyst && string.IsNullOrEmpty(options.TestPlanPath))
        {
            Console.WriteLine("Error: --testplan is required when using --skip-analyst.");
            return 1;
        }

        var configuration = CommandHelpers.LoadConfiguration(options.ConfigPath);
        var orchestrator = new OrchestratorRunner(configuration);

        try
        {
            var summary = await orchestrator.RunAsync(options);

            // Save summary
            var summaryPath = Path.Combine(options.OutputPath, "summary.json");
            var summaryJson = JsonSerializer.Serialize(summary, JsonSerializerOptionsProvider.Default);
            await File.WriteAllTextAsync(summaryPath, summaryJson);

            ConsoleFormatter.WritePhaseHeader("Orchestration Complete");

            var summaryItems = new List<(string Label, string Value)>
            {
                ("Status", summary.OverallStatus.ToString()),
                ("Tutorial", summary.TutorialName ?? summary.TutorialUrl ?? "N/A"),
                ("Total Duration", ConsoleFormatter.FormatElapsed(summary.Duration)),
                ("Email Report", summary.EmailSent ? "Sent" : "Not sent"),
                ("Output Directory", Path.GetFullPath(options.OutputPath))
            };

            // Add key file paths
            if (summary.Files.TestPlan != null)
                summaryItems.Add(("Test Plan", summary.Files.TestPlan));
            if (summary.Files.ValidationResult != null)
                summaryItems.Add(("Validation Result", summary.Files.ValidationResult));
            if (summary.Files.ValidationReport != null)
                summaryItems.Add(("Validation Report", summary.Files.ValidationReport));

            ConsoleFormatter.WriteEndSummary(summaryItems);

            return summary.OverallStatus == ValidationStatus.Passed ? 0 : 1;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
            return 1;
        }
    }

}

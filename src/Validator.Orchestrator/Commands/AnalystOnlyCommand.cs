using System.Diagnostics;
using Validator.Core;
using Validator.Orchestrator.Runners;

namespace Validator.Orchestrator.Commands;

/// <summary>
/// Handles the "analyst-only" CLI command.
/// Runs only the analyst to generate a testplan.
/// </summary>
internal static class AnalystOnlyCommand
{
    internal static async Task<int> RunAsync(string[] args)
    {
        var options = CommandHelpers.ParseOptions(args);

        if (string.IsNullOrEmpty(options.TutorialUrl))
        {
            Console.WriteLine("Error: --url is required for analyst-only command.");
            return 1;
        }

        var configuration = CommandHelpers.LoadConfiguration(options.ConfigPath);
        var analystRunner = new AnalystRunner(configuration);

        try
        {
            var stopwatch = Stopwatch.StartNew();

            ConsoleFormatter.WriteBanner(new (string, string)[]
            {
                ("Command", "analyst-only"),
                ("Tutorial URL", options.TutorialUrl),
                ("Output Directory", Path.GetFullPath(options.OutputPath))
            });

            var result = await analystRunner.RunAsync(options.TutorialUrl, options.OutputPath);

            ConsoleFormatter.WriteEndSummary(new (string, string)[]
            {
                ("Status", result.Success ? "OK" : "FAILED"),
                ("Duration", ConsoleFormatter.FormatElapsed(stopwatch.Elapsed)),
                ("Test Plan", result.Success ? (result.TestPlanPath ?? "N/A") : "N/A"),
                ("Tutorial", result.TutorialName ?? "N/A"),
                ("Error", result.Success ? "None" : (result.Error ?? "Unknown error"))
            });

            return result.Success ? 0 : 1;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
            return 1;
        }
    }
}

using System.Diagnostics;
using Validator.Core;
using Validator.Orchestrator.Runners;

namespace Validator.Orchestrator.Commands;

/// <summary>
/// Handles the "docker-only" CLI command.
/// Runs only the executor in Docker (requires an existing testplan).
/// </summary>
internal static class DockerOnlyCommand
{
    internal static async Task<int> RunAsync(string[] args)
    {
        var options = CommandHelpers.ParseOptions(args);

        if (string.IsNullOrEmpty(options.TestPlanPath))
        {
            Console.WriteLine("Error: --testplan is required for docker-only command.");
            return 1;
        }

        var configuration = CommandHelpers.LoadConfiguration(options.ConfigPath);
        var dockerRunner = new DockerRunner(configuration);

        try
        {
            var stopwatch = Stopwatch.StartNew();

            ConsoleFormatter.WriteBanner(new (string, string)[]
            {
                ("Command", "docker-only"),
                ("Test Plan", options.TestPlanPath),
                ("Output Directory", Path.GetFullPath(options.OutputPath)),
                ("Keep Containers", options.KeepContainers ? "Yes" : "No")
            });

            ConsoleFormatter.WritePhaseHeader("Starting Docker Environment", stopwatch);
            await dockerRunner.StartEnvironmentAsync(options.OutputPath);

            ConsoleFormatter.WritePhaseHeader("Running Executor", stopwatch);
            var result = await dockerRunner.RunExecutorAsync(options.TestPlanPath, options.OutputPath);

            if (!options.KeepContainers)
            {
                ConsoleFormatter.WritePhaseHeader("Stopping Docker Environment", stopwatch);
                await dockerRunner.StopEnvironmentAsync();
            }

            await dockerRunner.WriteDockerLogAsync(options.OutputPath);

            ConsoleFormatter.WriteEndSummary(new (string, string)[]
            {
                ("Status", result.Success ? "OK" : "FAILED"),
                ("Duration", ConsoleFormatter.FormatElapsed(stopwatch.Elapsed)),
                ("Output", Path.GetFullPath(options.OutputPath))
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

using Microsoft.Extensions.Configuration;
using Validator.Orchestrator.Models;

namespace Validator.Orchestrator.Commands;

/// <summary>
/// Shared helper methods for CLI command handlers.
/// </summary>
internal static class CommandHelpers
{
    /// <summary>
    /// Parses command-line arguments into orchestrator options.
    /// </summary>
    internal static OrchestratorOptions ParseOptions(string[] args)
    {
        var options = new OrchestratorOptions();

        for (int i = 0; i < args.Length; i++)
        {
            var arg = args[i].ToLowerInvariant();
            var hasNext = i + 1 < args.Length;

            switch (arg)
            {
                case "--url" or "-u":
                    if (hasNext) options.TutorialUrl = args[++i];
                    break;
                case "--testplan" or "-t":
                    if (hasNext) options.TestPlanPath = args[++i];
                    break;
                case "--output" or "-o":
                    if (hasNext) options.OutputPath = args[++i];
                    break;
                case "--config" or "-c":
                    if (hasNext) options.ConfigPath = args[++i];
                    break;
                case "--skip-analyst":
                    options.SkipAnalyst = true;
                    break;
                case "--keep-containers":
                    options.KeepContainers = true;
                    break;
                case "--local":
                    options.LocalExecution = true;
                    break;
                case "--persona":
                    if (hasNext)
                    {
                        var personaValue = args[++i].ToLowerInvariant();
                        if (personaValue is "junior" or "mid" or "senior")
                        {
                            options.Persona = personaValue;
                        }
                        else
                        {
                            Console.Error.WriteLine($"Warning: Unknown persona '{args[i]}'. Using 'mid' (default).");
                            Console.Error.WriteLine("Valid personas: junior, mid, senior");
                        }
                    }
                    break;
                case "--timeout":
                    if (hasNext && int.TryParse(args[++i], out var timeout))
                        options.TimeoutMinutes = timeout;
                    break;
            }
        }

        return options;
    }

    /// <summary>
    /// Loads configuration from appsettings.json and environment variables.
    /// </summary>
    internal static IConfiguration LoadConfiguration(string? configPath)
    {
        var builder = new ConfigurationBuilder();

        // Try multiple locations for configuration
        var locations = new[]
        {
            configPath,
            Path.Combine(Directory.GetCurrentDirectory(), "appsettings.json"),
            Path.Combine(AppContext.BaseDirectory, "appsettings.json")
        };

        foreach (var location in locations)
        {
            if (!string.IsNullOrEmpty(location) && File.Exists(location))
            {
                builder.AddJsonFile(location, optional: true);
                break;
            }
        }

        builder.AddEnvironmentVariables();

        return builder.Build();
    }
}

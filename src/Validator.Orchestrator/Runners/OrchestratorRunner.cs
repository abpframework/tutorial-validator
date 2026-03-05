using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Validator.Core;
using Validator.Core.Models.Enums;
using Validator.Core.Models.Results;
using Validator.Orchestrator.Models;

namespace Validator.Orchestrator.Runners;

/// <summary>
/// Main orchestrator that coordinates the Analyst and Executor.
/// </summary>
public class OrchestratorRunner
{
    private readonly IConfiguration _configuration;
    private readonly AnalystRunner _analystRunner;
    private readonly DockerRunner _dockerRunner;
    private readonly OutputOrganizer _outputOrganizer;
    private readonly EmailReportSender _emailReportSender;
    private readonly DiscordReportSender _discordReportSender;

    public OrchestratorRunner(IConfiguration configuration)
    {
        _configuration = configuration;
        _analystRunner = new AnalystRunner(configuration);
        _dockerRunner = new DockerRunner(configuration);
        _outputOrganizer = new OutputOrganizer();
        _emailReportSender = new EmailReportSender(configuration);
        _discordReportSender = new DiscordReportSender(configuration);
    }

    /// <summary>
    /// Run the full orchestration pipeline.
    /// </summary>
    public async Task<OrchestrationSummary> RunAsync(OrchestratorOptions options)
    {
        var pipelineStopwatch = Stopwatch.StartNew();

        var summary = new OrchestrationSummary
        {
            TutorialUrl = options.TutorialUrl,
            StartedAt = DateTime.UtcNow,
            Environment = await GetEnvironmentInfoAsync()
        };

        // Startup banner
        ConsoleFormatter.WriteBanner(new (string, string)[]
        {
            ("Tutorial URL", options.TutorialUrl ?? "N/A"),
            ("Output Directory", Path.GetFullPath(options.OutputPath)),
            ("Execution Mode", options.LocalExecution ? "Local" : "Docker"),
            ("Persona", options.Persona.ToUpperInvariant()),
            ("Skip Analyst", options.SkipAnalyst ? "Yes" : "No"),
            ("Keep Containers", options.KeepContainers ? "Yes" : "No"),
            ("Timeout", $"{options.TimeoutMinutes} minutes")
        });

        Console.WriteLine();

        ConsoleFormatter.WriteBanner(new (string, string)[]
        {
            ("OS", summary.Environment.OperatingSystem ?? "N/A"),
            ("Machine", summary.Environment.MachineName ?? "N/A"),
            (".NET SDK", summary.Environment.DotNetVersion ?? "N/A"),
            ("Docker", summary.Environment.DockerVersion ?? "N/A")
        });

        // Prepare output directory
        var outputPath = PrepareOutputDirectory(options.OutputPath);

        string testPlanPath;

        // Phase 1: Run Analyst (unless skipped)
        var phaseStopwatch = Stopwatch.StartNew();

        if (options.SkipAnalyst)
        {
            testPlanPath = options.TestPlanPath!;
            Console.WriteLine($"Skipping analyst, using existing testplan: {testPlanPath}");
            summary.AnalystSuccess = true;

            // Copy testplan to output if not already there
            var targetPath = Path.Combine(outputPath, "testplan.json");
            if (!Path.GetFullPath(testPlanPath).Equals(Path.GetFullPath(targetPath), StringComparison.OrdinalIgnoreCase))
            {
                File.Copy(testPlanPath, targetPath, overwrite: true);
            }
            testPlanPath = targetPath;
        }
        else
        {
            ConsoleFormatter.WritePhaseHeader("Phase 1: Running Analyst", pipelineStopwatch);

            var analystResult = await _analystRunner.RunAsync(options.TutorialUrl!, outputPath);
            summary.AnalystSuccess = analystResult.Success;

            if (!analystResult.Success)
            {
                summary.AnalystError = analystResult.Error;
                summary.OverallStatus = ValidationStatus.Failed;
                summary.CompletedAt = DateTime.UtcNow;
                summary.AnalystDuration = phaseStopwatch.Elapsed;
                Console.WriteLine($"Analyst failed: {analystResult.Error}");
                return summary;
            }

            testPlanPath = analystResult.TestPlanPath!;
            summary.TutorialName = analystResult.TutorialName;
            Console.WriteLine($"Test plan generated: {testPlanPath}");
        }

        summary.AnalystDuration = phaseStopwatch.Elapsed;
        phaseStopwatch.Restart();

        summary.Files.TestPlan = Path.GetRelativePath(outputPath, testPlanPath);

        // Phase 2: Run Executor
        ConsoleFormatter.WritePhaseHeader("Phase 2: Running Executor", pipelineStopwatch);

        ProcessResult executorResult;

        if (options.LocalExecution)
        {
            Console.WriteLine("Running executor locally (no Docker)...");
            executorResult = await RunLocalExecutorAsync(testPlanPath, outputPath, options.Persona, options.TimeoutMinutes);
        }
        else
        {
            Console.WriteLine("Starting Docker environment...");
            await _dockerRunner.StartEnvironmentAsync(outputPath);

            Console.WriteLine("Running executor in Docker...");
            _dockerRunner.TimeoutMinutes = options.TimeoutMinutes;
            executorResult = await _dockerRunner.RunExecutorAsync(testPlanPath, outputPath, options.Persona);

            if (!options.KeepContainers)
            {
                Console.WriteLine("Stopping Docker environment...");
                await _dockerRunner.StopEnvironmentAsync();
            }

            // Write accumulated Docker log to file
            await _dockerRunner.WriteDockerLogAsync(outputPath);
        }

        summary.ExecutorSuccess = executorResult.Success;

        if (!executorResult.Success)
        {
            summary.ExecutorError = executorResult.Error;
        }

        summary.ExecutorDuration = phaseStopwatch.Elapsed;
        phaseStopwatch.Restart();

        // Phase 3: Organize outputs
        ConsoleFormatter.WritePhaseHeader("Phase 3: Organizing Outputs", pipelineStopwatch);

        await _outputOrganizer.OrganizeAsync(outputPath, summary);

        // Determine overall status from validation result
        var validationResultPath = Path.Combine(outputPath, "results", "validation-result.json");
        if (File.Exists(validationResultPath))
        {
            try
            {
                var json = await File.ReadAllTextAsync(validationResultPath);
                var validationResult = JsonSerializer.Deserialize<ValidationResult>(json, JsonSerializerOptionsProvider.Default);
                summary.OverallStatus = validationResult?.Status ?? ValidationStatus.Failed;
                summary.Files.ValidationResult = "results/validation-result.json";
            }
            catch
            {
                summary.OverallStatus = ValidationStatus.Failed;
            }
        }
        else
        {
            summary.OverallStatus = executorResult.Success ? ValidationStatus.Passed : ValidationStatus.Failed;
        }

        var validationReportPath = Path.Combine(outputPath, "results", "validation-report.json");
        if (File.Exists(validationReportPath))
        {
            summary.Files.ValidationReport = "results/validation-report.json";
        }

        var generatedProjectPath = Path.Combine(outputPath, "generated-project");
        if (Directory.Exists(generatedProjectPath))
        {
            summary.Files.GeneratedProject = "generated-project";
        }

        summary.CompletedAt = DateTime.UtcNow;

        summary.OrganizeDuration = phaseStopwatch.Elapsed;
        phaseStopwatch.Restart();

        // Phase 4: Send Email Report (if enabled)
        ConsoleFormatter.WritePhaseHeader("Phase 4: Email Report", pipelineStopwatch);
        await _emailReportSender.SendIfEnabledAsync(outputPath, summary);

        summary.EmailDuration = phaseStopwatch.Elapsed;
        phaseStopwatch.Restart();

        // Phase 5: Send Discord Report (if enabled)
        ConsoleFormatter.WritePhaseHeader("Phase 5: Discord Report", pipelineStopwatch);
        await _discordReportSender.SendIfEnabledAsync(outputPath, summary);

        summary.DiscordDuration = phaseStopwatch.Elapsed;

        return summary;
    }

    private string PrepareOutputDirectory(string outputPath)
    {
        var fullPath = Path.GetFullPath(outputPath);

        // Create main output directory
        Directory.CreateDirectory(fullPath);

        // Create subdirectories
        Directory.CreateDirectory(Path.Combine(fullPath, "scraped"));
        Directory.CreateDirectory(Path.Combine(fullPath, "results"));
        Directory.CreateDirectory(Path.Combine(fullPath, "logs"));

        return fullPath;
    }

    private async Task<ProcessResult> RunLocalExecutorAsync(string testPlanPath, string outputPath, string persona, int timeoutMinutes = 120)
    {
        var executorPath = FindExecutorPath();
        var resultsPath = Path.Combine(outputPath, "results");
        var workDir = Path.Combine(outputPath, "workspace");

        Directory.CreateDirectory(workDir);

        var arguments = $"run --input \"{testPlanPath}\" --workdir \"{workDir}\" --output \"{resultsPath}\" --persona {persona}";

        // Pass the Orchestrator's appsettings.json so the Executor subprocess can read AI credentials
        var configPath = GetOrchestratorConfigPath();
        if (!string.IsNullOrEmpty(configPath))
        {
            arguments += $" --config \"{configPath}\"";
        }

        return await ProcessHelper.RunAsync(
            "dotnet",
            $"run --project \"{executorPath}\" -- {arguments}",
            Path.GetDirectoryName(executorPath)!,
            TimeSpan.FromMinutes(timeoutMinutes)
        );
    }

    private static string? GetOrchestratorConfigPath()
    {
        var locations = new[]
        {
            Path.Combine(Directory.GetCurrentDirectory(), "appsettings.json"),
            Path.Combine(AppContext.BaseDirectory, "appsettings.json"),
        };

        foreach (var location in locations)
        {
            if (File.Exists(location))
                return Path.GetFullPath(location);
        }

        return null;
    }

    private static string FindExecutorPath()
    {
        return ProjectPathResolver.FindProjectFile("Validator.Executor");
    }

    private async Task<OrchestratorEnvironmentInfo> GetEnvironmentInfoAsync()
    {
        var info = new OrchestratorEnvironmentInfo
        {
            OperatingSystem = Environment.OSVersion.ToString(),
            MachineName = Environment.MachineName
        };

        // Get .NET version
        var dotnetResult = await ProcessHelper.RunAsync("dotnet", "--version", Directory.GetCurrentDirectory(), TimeSpan.FromSeconds(10));
        if (dotnetResult.Success)
        {
            info.DotNetVersion = dotnetResult.Output.Trim();
        }

        // Get Docker version
        var dockerResult = await ProcessHelper.RunAsync("docker", "--version", Directory.GetCurrentDirectory(), TimeSpan.FromSeconds(10));
        if (dockerResult.Success)
        {
            info.DockerVersion = dockerResult.Output.Trim();
        }

        return info;
    }

}

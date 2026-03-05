using Validator.Core;
using Validator.Core.Models.Enums;
using Validator.Executor.Agent;
using Validator.Executor.Execution;
using Validator.Executor.Reporting;

namespace Validator.Executor.Commands;

/// <summary>
/// Handles the "run" CLI command.
/// Executes a TestPlan using the AI agent.
/// </summary>
internal static class RunCommand
{
    /// <summary>
    /// Runs the execute command with the provided arguments.
    /// </summary>
    internal static async Task RunAsync(string[] args)
    {
        string? inputPath = null;
        string? workDir = null;
        string outputDir = "results";
        string? configPath = null;
        var dryRun = false;
        var persona = DeveloperPersona.Mid;

        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--input" when i + 1 < args.Length:
                    inputPath = args[++i];
                    break;
                case "--workdir" when i + 1 < args.Length:
                    workDir = args[++i];
                    break;
                case "--output" when i + 1 < args.Length:
                    outputDir = args[++i];
                    break;
                case "--config" when i + 1 < args.Length:
                    configPath = args[++i];
                    break;
                case "--persona" when i + 1 < args.Length:
                    var personaValue = args[++i].ToLowerInvariant();
                    persona = personaValue switch
                    {
                        "junior" => DeveloperPersona.Junior,
                        "mid" => DeveloperPersona.Mid,
                        "senior" => DeveloperPersona.Senior,
                        _ => DeveloperPersona.Mid
                    };
                    if (personaValue is not ("junior" or "mid" or "senior"))
                    {
                        Console.Error.WriteLine($"Warning: Unknown persona '{args[i]}'. Using 'mid' (default).");
                        Console.Error.WriteLine("Valid personas: junior, mid, senior");
                    }
                    break;
                case "--dry-run":
                    dryRun = true;
                    break;
                default:
                    // Support positional testplan.json argument
                    if (inputPath == null && args[i].EndsWith(".json", StringComparison.OrdinalIgnoreCase))
                    {
                        inputPath = args[i];
                    }
                    break;
            }
        }

        if (string.IsNullOrWhiteSpace(inputPath))
        {
            Console.Error.WriteLine("Error: Input path is required.");
            Console.Error.WriteLine("Usage: dotnet run -- run --input <testplan.json> --workdir <directory>");
            Environment.Exit(1);
            return;
        }

        if (!File.Exists(inputPath))
        {
            Console.Error.WriteLine($"Error: TestPlan file not found: {inputPath}");
            Environment.Exit(1);
            return;
        }

        // Default working directory to current directory
        workDir ??= Directory.GetCurrentDirectory();
        var fullWorkDir = Path.GetFullPath(workDir);

        ConsoleFormatter.WritePhaseHeader("ABP Tutorial Executor");
        Console.WriteLine($"Input: {inputPath}");
        Console.WriteLine($"Working directory: {fullWorkDir}");
        Console.WriteLine($"Output directory: {outputDir}");
        Console.WriteLine($"Persona: {persona}");
        Console.WriteLine($"Dry-run: {dryRun}");
        Console.WriteLine();

        try
        {
            // Load TestPlan
            Console.WriteLine("Loading TestPlan...");
            var testPlan = await ExecutionOrchestrator.LoadTestPlanAsync(inputPath);
            Console.WriteLine($"  Tutorial: {testPlan.TutorialName}");
            Console.WriteLine($"  Steps: {testPlan.Steps.Count}");
            Console.WriteLine();

            // Load AI configuration and create agent
            Console.WriteLine("Loading AI configuration...");
            var aiConfig = AgentKernelFactory.LoadConfiguration(configPath);

            try
            {
                AgentKernelFactory.ValidateConfiguration(aiConfig);
            }
            catch (InvalidOperationException ex)
            {
                Console.Error.WriteLine($"Error: {ex.Message}");
                Console.Error.WriteLine();
                Console.Error.WriteLine("Please set the required environment variables or provide an appsettings.json with AI credentials.");
                Environment.Exit(1);
                return;
            }

            Console.WriteLine($"  Provider: {aiConfig.Provider}");
            Console.WriteLine($"  Model/Deployment: {aiConfig.DeploymentName}");
            Console.WriteLine();

            // Ensure working directory exists
            if (!Directory.Exists(fullWorkDir))
            {
                Console.WriteLine($"Creating working directory: {fullWorkDir}");
                Directory.CreateDirectory(fullWorkDir);
            }

            // Create Semantic Kernel with plugins and function call tracker
            Console.WriteLine("Initializing AI agent...");
            var kernelContext = AgentKernelFactory.CreateExecutorKernel(aiConfig, fullWorkDir);

            // Create agent with selected persona and orchestrator
            var agent = new StepExecutionAgent(kernelContext.Kernel, fullWorkDir, kernelContext.Tracker, persona, kernelContext.CommandPlugin);
            var orchestrator = new ExecutionOrchestrator(agent, new ResultReporter());
            Console.WriteLine($"  AI agent ready (persona: {persona})");
            Console.WriteLine();

            // Execute
            var result = await orchestrator.ExecuteAndSaveAsync(testPlan, fullWorkDir, outputDir, dryRun);

            // Exit with appropriate code
            Environment.Exit(result.Status == ValidationStatus.Passed ? 0 : 1);
        }
        catch (FileNotFoundException ex)
        {
            ConsoleFormatter.WritePhaseHeader("File Not Found");
            Console.Error.WriteLine($"Error: {ex.Message}");
            Environment.Exit(1);
        }
        catch (Exception ex)
        {
            ConsoleFormatter.WritePhaseHeader("Execution Failed");
            Console.Error.WriteLine($"Error: {ex.Message}");
            Console.Error.WriteLine(ex.StackTrace);
            Environment.Exit(1);
        }
    }
}

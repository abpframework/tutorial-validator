using Validator.Analyst.Analysis;
using Validator.Core;

namespace Validator.Analyst.Commands;

/// <summary>
/// Handles the "analyze" CLI command.
/// Analyzes previously scraped content and generates a TestPlan.
/// </summary>
internal static class AnalyzeCommand
{
    /// <summary>
    /// Runs the analyze command with the provided arguments.
    /// </summary>
    internal static async Task RunAsync(string[] args)
    {
        string? inputPath = null;
        string? outputPath = null;
        string? configPath = null;
        var targetSteps = 50;
        var maxSteps = 55;

        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--input" when i + 1 < args.Length:
                    inputPath = args[++i];
                    break;
                case "--output" when i + 1 < args.Length:
                    outputPath = args[++i];
                    break;
                case "--config" when i + 1 < args.Length:
                    configPath = args[++i];
                    break;
                case "--target-steps" when i + 1 < args.Length:
                    targetSteps = int.Parse(args[++i]);
                    break;
                case "--max-steps" when i + 1 < args.Length:
                    maxSteps = int.Parse(args[++i]);
                    break;
            }
        }

        if (string.IsNullOrWhiteSpace(inputPath))
        {
            Console.Error.WriteLine("Error: Input path is required.");
            Console.Error.WriteLine("Usage: dotnet run -- analyze --input <scraped-dir-or-json>");
            Environment.Exit(1);
            return;
        }

        // Resolve input path to tutorial.json
        var tutorialJsonPath = inputPath;
        if (Directory.Exists(inputPath))
        {
            tutorialJsonPath = Path.Combine(inputPath, "tutorial.json");
        }

        if (!File.Exists(tutorialJsonPath))
        {
            Console.Error.WriteLine($"Error: Could not find tutorial.json at: {tutorialJsonPath}");
            Environment.Exit(1);
            return;
        }

        // Default output path
        outputPath ??= Path.Combine(Path.GetDirectoryName(tutorialJsonPath) ?? ".", "testplan.json");

        Console.WriteLine($"Loading tutorial from: {tutorialJsonPath}");
        Console.WriteLine($"Output: {outputPath}");
        Console.WriteLine();

        try
        {
            // Load AI configuration
            var aiConfig = CommandHelpers.LoadAndValidateAIConfig(configPath);

            Console.WriteLine($"  Provider: {aiConfig.Provider}");
            Console.WriteLine($"  Model/Deployment: {aiConfig.DeploymentName}");
            Console.WriteLine();

            // Create Semantic Kernel
            var kernel = KernelFactory.CreateFromConfiguration(aiConfig);
            var compactionOptions = new StepCompactionOptions
            {
                TargetStepCount = targetSteps,
                MaxStepCount = maxSteps
            };

            // Load scraped tutorial
            var tutorial = await TutorialAnalyzer.LoadTutorialAsync(tutorialJsonPath);

            // Run analysis
            var analyzer = new TutorialAnalyzer(kernel, compactionOptions);
            var testPlan = await analyzer.AnalyzeAsync(tutorial);

            // Save TestPlan
            await TutorialAnalyzer.SaveTestPlanAsync(testPlan, outputPath);

            ConsoleFormatter.WritePhaseHeader("Analysis Complete");
            Console.WriteLine($"TestPlan saved to: {outputPath}");
            Console.WriteLine($"Total steps: {testPlan.Steps.Count}");
        }
        catch (Exception ex)
        {
            ConsoleFormatter.WritePhaseHeader("Analysis Failed");
            Console.Error.WriteLine($"Error: {ex.Message}");
            Console.Error.WriteLine(ex.StackTrace);
            Environment.Exit(1);
        }
    }
}

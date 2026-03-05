using System.Text.Json;
using Microsoft.SemanticKernel;
using Validator.Analyst.Scraping.Models;
using Validator.Core;
using Validator.Core.Models;
using Validator.Core.Models.Steps;

namespace Validator.Analyst.Analysis;

/// <summary>
/// Main orchestrator for tutorial analysis.
/// Coordinates the extraction and classification of tutorial steps.
/// </summary>
public class TutorialAnalyzer
{
    private readonly Kernel _kernel;
    private readonly StepExtractor _stepExtractor;
    private readonly StepCompactionOptions _compactionOptions;

    /// <summary>
    /// Creates a new TutorialAnalyzer with the provided Semantic Kernel.
    /// </summary>
    public TutorialAnalyzer(Kernel kernel, StepCompactionOptions? compactionOptions = null)
    {
        _kernel = kernel;
        _stepExtractor = new StepExtractor(kernel);
        _compactionOptions = compactionOptions ?? new StepCompactionOptions();
    }

    /// <summary>
    /// Analyzes a scraped tutorial and produces a TestPlan.
    /// </summary>
    /// <param name="tutorial">The scraped tutorial content</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>A TestPlan ready for execution</returns>
    public async Task<TestPlan> AnalyzeAsync(
        ScrapedTutorial tutorial,
        CancellationToken cancellationToken = default)
    {
        Console.WriteLine($"Analyzing tutorial: {tutorial.Title}");
        Console.WriteLine($"Total pages: {tutorial.TotalPages}");
        Console.WriteLine();

        // Step 1: Detect configuration
        Console.WriteLine("Detecting tutorial configuration...");
        var configuration = ConfigurationDetector.DetectConfiguration(tutorial);
        var tutorialName = ConfigurationDetector.ExtractTutorialName(tutorial);
        var abpVersion = ConfigurationDetector.DetectAbpVersion(tutorial);

        Console.WriteLine($"  UI: {configuration.Ui}");
        Console.WriteLine($"  Database: {configuration.Database}");
        Console.WriteLine($"  DB Provider: {configuration.DbProvider ?? "default"}");
        Console.WriteLine($"  ABP Version: {abpVersion}");
        Console.WriteLine();

        // Step 2: Extract steps from all pages
        Console.WriteLine("Extracting steps from tutorial pages...");
        var rawSteps = await _stepExtractor.ExtractAllStepsAsync(
            tutorial,
            configuration,
            cancellationToken);

        Console.WriteLine($"Extracted {rawSteps.Count} raw steps");
        foreach (var group in rawSteps.GroupBy(s => s.GetType().Name).OrderBy(g => g.Key))
        {
            Console.WriteLine($"  - {group.Key}: {group.Count()}");
        }
        Console.WriteLine();

        // Step 3: Validate and normalize steps
        Console.WriteLine("Validating and normalizing steps...");
        var validationResult = StepNormalizer.ValidateAndNormalize(rawSteps);

        if (validationResult.Issues.Count > 0)
        {
            Console.WriteLine("Validation issues found:");
            foreach (var issue in validationResult.Issues)
            {
                Console.WriteLine($"  - {issue}");
            }
            Console.WriteLine();
        }

        Console.WriteLine($"Final step count (normalized): {validationResult.Steps.Count}");
        foreach (var group in validationResult.Steps.GroupBy(s => s.GetType().Name).OrderBy(g => g.Key))
        {
            Console.WriteLine($"  - normalized {group.Key}: {group.Count()}");
        }

        // Step 3.5: Check if project creation step is needed and prepend if necessary
        if (ProjectNameInferrer.NeedsProjectCreationStep(validationResult.Steps))
        {
            Console.WriteLine("No project creation step detected. Attempting to prepend one...");
            
            var projectName = ProjectNameInferrer.InferProjectNameFromSteps(validationResult.Steps);
            
            if (!string.IsNullOrEmpty(projectName))
            {
                Console.WriteLine($"  Inferred project name: {projectName}");
                
                // Build the "abp new" command from configuration
                var abpNewCommand = BuildProjectCreationCommand(projectName, configuration);
                Console.WriteLine($"  Command: {abpNewCommand}");
                
                // Create the project creation step
                var projectCreationStep = new CommandStep
                {
                    StepId = 0, // Will be renumbered
                    Description = $"Create new ABP solution named {projectName}",
                    Command = abpNewCommand,
                    Expects = new CommandExpectation
                    {
                        ExitCode = 0,
                        Creates = [projectName]
                    }
                };
                
                // Prepend to the beginning of the steps list
                validationResult.Steps.Insert(0, projectCreationStep);
                
                // Renumber all steps
                for (int i = 0; i < validationResult.Steps.Count; i++)
                {
                    validationResult.Steps[i].StepId = i + 1;
                }
                
                Console.WriteLine($"  Prepended project creation step. New step count: {validationResult.Steps.Count}");
            }
            else
            {
                Console.WriteLine("  Warning: Could not infer project name from steps. Project creation step not added.");
                Console.WriteLine("  The test plan may fail if it requires a pre-existing project.");
            }
            
            Console.WriteLine();
        }

        // Step 4: Compact normalized steps with step-type-safe rules
        var compactedSteps = StepCompactor.Compact(validationResult.Steps, _compactionOptions);
        Console.WriteLine($"Final step count (compacted): {compactedSteps.Count}");
        foreach (var group in compactedSteps.GroupBy(s => s.GetType().Name).OrderBy(g => g.Key))
        {
            Console.WriteLine($"  - compacted {group.Key}: {group.Count()}");
        }

        // Step 5: Build TestPlan
        var testPlan = new TestPlan
        {
            TutorialName = tutorialName,
            TutorialUrl = tutorial.SourceUrl,
            AbpVersion = abpVersion,
            Configuration = configuration,
            Steps = compactedSteps
        };

        return testPlan;
    }

    /// <summary>
    /// Analyzes a scraped tutorial and returns the TestPlan as JSON.
    /// </summary>
    /// <param name="tutorial">The scraped tutorial content</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>TestPlan serialized as JSON</returns>
    public async Task<string> AnalyzeToJsonAsync(
        ScrapedTutorial tutorial,
        CancellationToken cancellationToken = default)
    {
        var testPlan = await AnalyzeAsync(tutorial, cancellationToken);
        return JsonSerializer.Serialize(testPlan, JsonSerializerOptionsProvider.Default);
    }

    /// <summary>
    /// Loads a ScrapedTutorial from a JSON file.
    /// </summary>
    /// <param name="filePath">Path to the tutorial.json file</param>
    /// <returns>Deserialized ScrapedTutorial</returns>
    public static async Task<ScrapedTutorial> LoadTutorialAsync(string filePath)
    {
        var json = await File.ReadAllTextAsync(filePath);
        var tutorial = JsonSerializer.Deserialize<ScrapedTutorial>(json, JsonSerializerOptionsProvider.Default);
        
        return tutorial ?? throw new InvalidOperationException($"Failed to deserialize tutorial from {filePath}");
    }

    /// <summary>
    /// Saves a TestPlan to a JSON file.
    /// </summary>
    /// <param name="testPlan">The TestPlan to save</param>
    /// <param name="filePath">Output file path</param>
    public static async Task SaveTestPlanAsync(TestPlan testPlan, string filePath)
    {
        var json = JsonSerializer.Serialize(testPlan, JsonSerializerOptionsProvider.Default);
        await File.WriteAllTextAsync(filePath, json);
    }

    /// <summary>
    /// Builds the "abp new" command with appropriate flags based on tutorial configuration.
    /// </summary>
    /// <param name="projectName">Name of the project to create</param>
    /// <param name="configuration">Tutorial configuration</param>
    /// <returns>Complete "abp new" command string</returns>
    private static string BuildProjectCreationCommand(string projectName, TutorialConfiguration configuration)
    {
        var command = $"abp new {projectName}";
        
        // Add UI flag
        if (!string.IsNullOrWhiteSpace(configuration.Ui))
        {
            command += $" -u {configuration.Ui.ToLowerInvariant()}";
        }
        
        // Add database flag
        if (!string.IsNullOrWhiteSpace(configuration.Database))
        {
            command += $" -d {configuration.Database.ToLowerInvariant()}";
        }
        
        // Add database provider flag
        if (!string.IsNullOrWhiteSpace(configuration.DbProvider))
        {
            command += $" --dbms {configuration.DbProvider.ToLowerInvariant()}";
        }
        
        // Add explicit output directory so ABP creates the project in a subdirectory.
        // ABP Studio CLI (v9+) outputs directly to the current directory without -o.
        command += $" -o {projectName}";
        
        return command;
    }
}

using System.Text.Json;
using Validator.Core;
using Validator.Core.Models;
using Validator.Core.Models.Assertions;
using Validator.Core.Models.Enums;
using Validator.Core.Models.Results;
using Validator.Core.Models.Steps;
using Validator.Executor.Agent;
using Validator.Executor.Reporting;

namespace Validator.Executor.Execution;

/// <summary>
/// Orchestrates the execution of a TestPlan using the AI agent.
/// </summary>
public class ExecutionOrchestrator
{
    private readonly ResultReporter _reporter;
    private readonly StepExecutionAgent _agent;

    /// <summary>
    /// Creates a new ExecutionOrchestrator with an AI agent.
    /// </summary>
    /// <param name="agent">The AI agent for step execution.</param>
    /// <param name="reporter">The result reporter.</param>
    public ExecutionOrchestrator(StepExecutionAgent agent, ResultReporter reporter)
    {
        _agent = agent;
        _reporter = reporter;
    }

    /// <summary>
    /// Loads a TestPlan from a JSON file.
    /// </summary>
    /// <param name="filePath">Path to the testplan.json file.</param>
    /// <returns>The deserialized TestPlan.</returns>
    public static async Task<TestPlan> LoadTestPlanAsync(string filePath)
    {
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException($"TestPlan file not found: {filePath}", filePath);
        }

        var json = await File.ReadAllTextAsync(filePath);
        var testPlan = JsonSerializer.Deserialize<TestPlan>(json, JsonSerializerOptionsProvider.Default);

        return testPlan ?? throw new InvalidOperationException("Failed to deserialize TestPlan");
    }

    /// <summary>
    /// Executes a TestPlan and returns the validation result.
    /// </summary>
    /// <param name="testPlan">The test plan to execute.</param>
    /// <param name="workingDirectory">The root working directory.</param>
    /// <param name="dryRun">If true, log what would be executed without actually running.</param>
    /// <returns>The validation result.</returns>
    public async Task<ValidationResult> ExecuteAsync(TestPlan testPlan, string workingDirectory, bool dryRun = false)
    {
        var context = new ExecutionContext
        {
            WorkingDirectory = Path.GetFullPath(workingDirectory),
            TestPlan = testPlan,
            Environment = ExecutionContext.CollectEnvironmentInfo(),
            DryRun = dryRun
        };

        ConsoleFormatter.WritePhaseHeader("Executing Test Plan");
        Console.WriteLine($"Tutorial: {testPlan.TutorialName}");
        Console.WriteLine($"Working directory: {context.WorkingDirectory}");
        Console.WriteLine($"Execution mode: {(dryRun ? "Dry-run" : "AI Agent")}");
        if (!dryRun)
        {
            Console.WriteLine($"Persona: {_agent.Persona}");
        }
        Console.WriteLine($"Steps: {testPlan.Steps.Count}");
        Console.WriteLine();

        // Normalize steps before execution (e.g., ensure abp new has -o flag)
        StepPreprocessor.NormalizeStepsForExecution(testPlan.Steps);

        var stepResults = new List<StepResult>();
        var orderedSteps = testPlan.Steps.OrderBy(s => s.StepId).ToList();

        try
        {
            foreach (var step in orderedSteps)
            {
                context.CurrentStepId = step.StepId;
                var stepType = StepTypeMapper.GetStepTypeName(step);

                Console.WriteLine($"Step {step.StepId}: {stepType}");
                if (step.Description != null)
                {
                    Console.WriteLine($"  Description: {step.Description}");
                }

                StepResult result;

                try
                {
                    // Before executing HTTP assertion steps, inject the base URL
                    // from a running background process if available.
                    InjectBackgroundBaseUrl(step);

                    // Use AI agent for all step types (including long-running commands).
                    // The agent discovers the correct working directory by exploring the
                    // workspace, which avoids the path resolution issues that occur when
                    // the orchestrator handles commands directly.
                    result = await _agent.ExecuteStepAsync(step);

                    stepResults.Add(result);

                    var statusIcon = result.Status switch
                    {
                        StepExecutionStatus.Success => "[OK]",
                        StepExecutionStatus.Failed => "[FAILED]",
                        StepExecutionStatus.Skipped => "[SKIPPED]",
                        StepExecutionStatus.Pending => "[PENDING]",
                        StepExecutionStatus.Running => "[RUNNING]",
                        _ => "[?]"
                    };

                    Console.WriteLine($"  {statusIcon} {result.Details}");

                    // After HTTP assertion steps complete, stop any background process
                    // if the next step is NOT another HTTP assertion (the server is no longer needed).
                    StopBackgroundProcessIfDone(step, orderedSteps);

                    // Fail fast - stop on first failure
                    if (result.Status == StepExecutionStatus.Failed)
                    {
                        Console.WriteLine();
                        Console.WriteLine("Execution stopped due to step failure.");
                        if (result.ErrorMessage != null)
                        {
                            Console.WriteLine($"Error: {result.ErrorMessage}");
                        }
                        break;
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"  [ERROR] Exception: {ex.Message}");

                    stepResults.Add(new StepResult
                    {
                        StepId = step.StepId,
                        StepType = StepTypeMapper.GetStepType(step),
                        Status = StepExecutionStatus.Failed,
                        StartedAt = DateTime.UtcNow,
                        CompletedAt = DateTime.UtcNow,
                        ErrorMessage = ex.Message,
                        ErrorOutput = ex.ToString()
                    });

                    Console.WriteLine("  Execution stopped due to exception.");
                    break;
                }

                Console.WriteLine();
            }
        }
        finally
        {
            // Always clean up any running background process at the end
            _agent.CommandPlugin?.StopBackgroundProcess();
        }

        // Mark remaining steps as skipped
        var executedStepIds = stepResults.Select(r => r.StepId).ToHashSet();
        foreach (var step in orderedSteps.Where(s => !executedStepIds.Contains(s.StepId)))
        {
            stepResults.Add(new StepResult
            {
                StepId = step.StepId,
                StepType = StepTypeMapper.GetStepType(step),
                Status = StepExecutionStatus.Skipped,
                Details = "Skipped due to earlier failure"
            });
        }

        return _reporter.BuildResult(testPlan, stepResults, context.StartedAt, context.Environment);
    }

    /// <summary>
    /// Executes a TestPlan and saves results to the output directory.
    /// </summary>
    public async Task<ValidationResult> ExecuteAndSaveAsync(
        TestPlan testPlan,
        string workingDirectory,
        string outputDirectory,
        bool dryRun = false)
    {
        var result = await ExecuteAsync(testPlan, workingDirectory, dryRun);

        Directory.CreateDirectory(outputDirectory);

        var resultPath = Path.Combine(outputDirectory, "validation-result.json");
        await _reporter.SaveResultAsync(result, resultPath);
        Console.WriteLine($"Result saved to: {resultPath}");

        var report = _reporter.BuildReport(result);
        var reportPath = Path.Combine(outputDirectory, "validation-report.json");
        await _reporter.SaveReportAsync(report, reportPath);
        Console.WriteLine($"Report saved to: {reportPath}");

        _reporter.PrintSummary(result);

        return result;
    }

    /// <summary>
    /// Injects the base URL from a running background process into HTTP assertion URLs.
    /// Replaces placeholder ports and resolves relative URLs against the actual server address.
    /// </summary>
    private void InjectBackgroundBaseUrl(TutorialStep step)
    {
        var commandPlugin = _agent.CommandPlugin;
        if (commandPlugin is not { HasBackgroundProcess: true } || commandPlugin.BackgroundProcessBaseUrl == null)
            return;

        if (step is not ExpectationStep expectation)
            return;

        var baseUrl = commandPlugin.BackgroundProcessBaseUrl;

        foreach (var assertion in expectation.Assertions)
        {
            if (assertion is not HttpAssertion http) continue;

            var originalUrl = http.Url;

            // Replace placeholder patterns like "https://localhost:<port>/..."
            if (http.Url.Contains("<port>", StringComparison.OrdinalIgnoreCase))
            {
                // Extract port from base URL (e.g., "https://localhost:44312" -> "44312")
                if (Uri.TryCreate(baseUrl, UriKind.Absolute, out var baseUri))
                {
                    http.Url = http.Url.Replace("<port>", baseUri.Port.ToString(), StringComparison.OrdinalIgnoreCase);
                }
            }
            // Resolve relative URLs (e.g., "/api/app/book" -> "https://localhost:44312/api/app/book")
            else if (http.Url.StartsWith('/'))
            {
                http.Url = baseUrl.TrimEnd('/') + http.Url;
            }

            if (http.Url != originalUrl)
            {
                Console.WriteLine($"  [URL] Resolved: {originalUrl} -> {http.Url}");
            }
        }
    }

    /// <summary>
    /// Stops the background process if the current step is an HTTP assertion step
    /// and the next step is NOT another HTTP assertion (meaning the server is no longer needed).
    /// </summary>
    private void StopBackgroundProcessIfDone(TutorialStep currentStep, List<TutorialStep> orderedSteps)
    {
        var commandPlugin = _agent.CommandPlugin;
        if (commandPlugin is not { HasBackgroundProcess: true })
            return;

        // Only act after expectation steps that contain HTTP assertions
        if (currentStep is not ExpectationStep expectation)
            return;

        var hasHttpAssertion = expectation.Assertions.Any(a => a is HttpAssertion);
        if (!hasHttpAssertion)
            return;

        // Check if the next step is also an HTTP assertion step
        var currentIndex = orderedSteps.FindIndex(s => s.StepId == currentStep.StepId);
        if (currentIndex >= 0 && currentIndex + 1 < orderedSteps.Count)
        {
            var nextStep = orderedSteps[currentIndex + 1];
            if (nextStep is ExpectationStep nextExpectation
                && nextExpectation.Assertions.Any(a => a is HttpAssertion))
            {
                // Next step also has HTTP assertions -- keep the server alive
                return;
            }
        }

        // No more HTTP assertions follow -- stop the background process
        Console.WriteLine("  [LIFECYCLE] Stopping background process (no more HTTP assertions follow)");
        commandPlugin.StopBackgroundProcess();
    }
}

using System.Text.Json;
using Validator.Core;
using Validator.Core.Models;
using Validator.Core.Models.Enums;
using Validator.Core.Models.Results;

namespace Validator.Executor.Reporting;

/// <summary>
/// Builds and serializes validation results and reports.
/// </summary>
public class ResultReporter
{
    /// <summary>
    /// Creates a ValidationResult from the execution context and step results.
    /// </summary>
    /// <param name="testPlan">The executed test plan.</param>
    /// <param name="stepResults">The results of each step.</param>
    /// <param name="startedAt">When execution started.</param>
    /// <param name="environment">Environment information.</param>
    /// <returns>A ValidationResult summarizing the execution.</returns>
    public ValidationResult BuildResult(
        TestPlan testPlan,
        IReadOnlyList<StepResult> stepResults,
        DateTime startedAt,
        EnvironmentInfo? environment = null)
    {
        var completedAt = DateTime.UtcNow;
        var passedSteps = stepResults.Count(r => r.Status == StepExecutionStatus.Success);
        var failedSteps = stepResults.Count(r => r.Status == StepExecutionStatus.Failed);
        var skippedSteps = stepResults.Count(r => r.Status == StepExecutionStatus.Skipped);
        var pendingSteps = stepResults.Count(r => r.Status == StepExecutionStatus.Pending);

        var failedStep = stepResults.FirstOrDefault(r => r.Status == StepExecutionStatus.Failed);
        var overallStatus = failedStep == null ? ValidationStatus.Passed : ValidationStatus.Failed;

        return new ValidationResult
        {
            TutorialName = testPlan.TutorialName,
            TutorialUrl = testPlan.TutorialUrl,
            AbpVersion = testPlan.AbpVersion,
            Configuration = testPlan.Configuration,
            Status = overallStatus,
            StartedAt = startedAt,
            CompletedAt = completedAt,
            TotalSteps = testPlan.Steps.Count,
            PassedSteps = passedSteps,
            FailedSteps = failedSteps,
            SkippedSteps = skippedSteps + pendingSteps, // Treat pending as skipped in summary
            StepResults = stepResults.ToList(),
            FailedAtStepId = failedStep?.StepId,
            Environment = environment
        };
    }

    /// <summary>
    /// Creates a ValidationReport from a ValidationResult.
    /// </summary>
    /// <param name="result">The validation result.</param>
    /// <returns>A ValidationReport for notifications.</returns>
    public ValidationReport BuildReport(ValidationResult result)
    {
        var diagnostics = result.Status == ValidationStatus.Failed
            ? BuildFailureDiagnostics(result)
            : null;

        return new ValidationReport
        {
            Result = result,
            GeneratedAt = DateTime.UtcNow,
            FailureDiagnostics = diagnostics
        };
    }

    /// <summary>
    /// Serializes a ValidationResult to JSON.
    /// </summary>
    public string SerializeResult(ValidationResult result)
    {
        return JsonSerializer.Serialize(result, JsonSerializerOptionsProvider.Default);
    }

    /// <summary>
    /// Serializes a ValidationReport to JSON.
    /// </summary>
    public string SerializeReport(ValidationReport report)
    {
        return JsonSerializer.Serialize(report, JsonSerializerOptionsProvider.Default);
    }

    /// <summary>
    /// Saves the ValidationResult to a file.
    /// </summary>
    public async Task SaveResultAsync(ValidationResult result, string filePath)
    {
        var json = SerializeResult(result);
        await File.WriteAllTextAsync(filePath, json);
    }

    /// <summary>
    /// Saves the ValidationReport to a file.
    /// </summary>
    public async Task SaveReportAsync(ValidationReport report, string filePath)
    {
        var json = SerializeReport(report);
        await File.WriteAllTextAsync(filePath, json);
    }

    /// <summary>
    /// Prints a summary of the validation result to the console.
    /// </summary>
    public void PrintSummary(ValidationResult result)
    {
        ConsoleFormatter.WritePhaseHeader("Validation Result");
        Console.WriteLine($"Tutorial: {result.TutorialName}");
        Console.WriteLine($"ABP Version: {result.AbpVersion}");
        Console.WriteLine($"Status: {result.Status}");
        Console.WriteLine($"Duration: {result.Duration?.TotalSeconds:F1}s");
        Console.WriteLine();
        Console.WriteLine($"Steps: {result.TotalSteps} total");
        Console.WriteLine($"  - Passed:  {result.PassedSteps}");
        Console.WriteLine($"  - Failed:  {result.FailedSteps}");
        Console.WriteLine($"  - Skipped: {result.SkippedSteps}");

        if (result.FailedAtStepId.HasValue)
        {
            var failedStep = result.StepResults.FirstOrDefault(s => s.StepId == result.FailedAtStepId);
            Console.WriteLine();
            Console.WriteLine($"Failed at step {result.FailedAtStepId}: {failedStep?.Details}");
            if (failedStep?.ErrorMessage != null)
            {
                Console.WriteLine($"Error: {failedStep.ErrorMessage}");
            }
        }
    }

    private static List<FailureDiagnostic>? BuildFailureDiagnostics(ValidationResult result)
    {
        var failedSteps = result.StepResults.Where(r => r.Status == StepExecutionStatus.Failed);
        
        return failedSteps.Select(step => new FailureDiagnostic
        {
            StepId = step.StepId,
            StepDescription = step.Details,
            Classification = ClassifyFailure(step),
            Explanation = step.ErrorMessage ?? "Step failed without specific error message",
            SuggestedFix = null, // Executor doesn't suggest fixes
            ErrorOutput = step.ErrorOutput
        }).ToList();
    }

    private static FailureClassification ClassifyFailure(StepResult step)
    {
        // Basic classification based on error output
        // This can be enhanced later with more sophisticated analysis
        var errorOutput = step.ErrorOutput?.ToLowerInvariant() ?? "";
        var errorMessage = step.ErrorMessage?.ToLowerInvariant() ?? "";

        if (errorOutput.Contains("command not found") || errorOutput.Contains("not recognized"))
        {
            return FailureClassification.EnvironmentIssue;
        }

        if (errorOutput.Contains("abp") && (errorOutput.Contains("deprecated") || errorOutput.Contains("obsolete")))
        {
            return FailureClassification.CliChanged;
        }

        if (errorOutput.Contains("file not found") || errorMessage.Contains("does not exist"))
        {
            return FailureClassification.TemplateChanged;
        }

        return FailureClassification.Unknown;
    }
}

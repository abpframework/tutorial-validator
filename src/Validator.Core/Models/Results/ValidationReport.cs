using Validator.Core.Models.Enums;

namespace Validator.Core.Models.Results;

/// <summary>
/// Final validation report for notifications (email, Discord).
/// </summary>
public class ValidationReport
{
    /// <summary>
    /// The validation result.
    /// </summary>
    public required ValidationResult Result { get; set; }

    /// <summary>
    /// Report generation timestamp.
    /// </summary>
    public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Subject line for email notifications.
    /// </summary>
    public string Subject => $"ABP Tutorials Execution Report [{(Result.Status == ValidationStatus.Passed ? "working" : "not working")}]";

    /// <summary>
    /// Human-readable summary of the validation.
    /// </summary>
    public string Summary => GenerateSummary();

    /// <summary>
    /// Detailed failure diagnostics (if any).
    /// </summary>
    public List<FailureDiagnostic>? FailureDiagnostics { get; set; }

    private string GenerateSummary()
    {
        var status = Result.Status == ValidationStatus.Passed ? "SUCCESS" : "FAILURE";
        var duration = Result.Duration?.TotalMinutes.ToString("F1") ?? "N/A";

        return $"""
            Tutorial: {Result.TutorialName}
            ABP Version: {Result.AbpVersion}
            Configuration: UI={Result.Configuration.Ui}, DB={Result.Configuration.Database}
            Status: {status}
            Duration: {duration} minutes
            Steps: {Result.PassedSteps}/{Result.TotalSteps} passed
            """;
    }
}

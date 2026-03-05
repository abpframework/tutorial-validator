using Validator.Core.Models.Enums;

namespace Validator.Core.Models.Results;

/// <summary>
/// Result of executing a single tutorial step.
/// </summary>
public class StepResult
{
    /// <summary>
    /// Reference to the step that was executed.
    /// </summary>
    public int StepId { get; set; }

    /// <summary>
    /// Type of the step that was executed.
    /// </summary>
    public StepType StepType { get; set; }

    /// <summary>
    /// Execution status of the step.
    /// </summary>
    public StepExecutionStatus Status { get; set; }

    /// <summary>
    /// When the step started executing.
    /// </summary>
    public DateTime? StartedAt { get; set; }

    /// <summary>
    /// When the step completed.
    /// </summary>
    public DateTime? CompletedAt { get; set; }

    /// <summary>
    /// Duration of the step execution.
    /// </summary>
    public TimeSpan? Duration => CompletedAt.HasValue && StartedAt.HasValue
        ? CompletedAt.Value - StartedAt.Value
        : null;

    /// <summary>
    /// Exit code for command steps.
    /// </summary>
    public int? ExitCode { get; set; }

    /// <summary>
    /// Standard output captured during execution.
    /// </summary>
    public string? Output { get; set; }

    /// <summary>
    /// Standard error captured during execution.
    /// </summary>
    public string? ErrorOutput { get; set; }

    /// <summary>
    /// Error message if the step failed.
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Additional details or diagnostics.
    /// </summary>
    public string? Details { get; set; }
}

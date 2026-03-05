using Validator.Core.Models.Enums;

namespace Validator.Core.Models.Results;

/// <summary>
/// Overall result of validating a tutorial.
/// </summary>
public class ValidationResult
{
    /// <summary>
    /// Name of the tutorial that was validated.
    /// </summary>
    public required string TutorialName { get; set; }

    /// <summary>
    /// URL of the tutorial.
    /// </summary>
    public required string TutorialUrl { get; set; }

    /// <summary>
    /// ABP version that was tested.
    /// </summary>
    public required string AbpVersion { get; set; }

    /// <summary>
    /// Configuration used for the validation.
    /// </summary>
    public required TutorialConfiguration Configuration { get; set; }

    /// <summary>
    /// Overall validation status.
    /// </summary>
    public ValidationStatus Status { get; set; }

    /// <summary>
    /// When the validation started.
    /// </summary>
    public DateTime StartedAt { get; set; }

    /// <summary>
    /// When the validation completed.
    /// </summary>
    public DateTime? CompletedAt { get; set; }

    /// <summary>
    /// Total duration of the validation.
    /// </summary>
    public TimeSpan? Duration => CompletedAt.HasValue
        ? CompletedAt.Value - StartedAt
        : null;

    /// <summary>
    /// Total number of steps in the test plan.
    /// </summary>
    public int TotalSteps { get; set; }

    /// <summary>
    /// Number of steps that passed.
    /// </summary>
    public int PassedSteps { get; set; }

    /// <summary>
    /// Number of steps that failed.
    /// </summary>
    public int FailedSteps { get; set; }

    /// <summary>
    /// Number of steps that were skipped.
    /// </summary>
    public int SkippedSteps { get; set; }

    /// <summary>
    /// Results for each step.
    /// </summary>
    public required List<StepResult> StepResults { get; set; }

    /// <summary>
    /// The step where failure occurred (if any).
    /// </summary>
    public int? FailedAtStepId { get; set; }

    /// <summary>
    /// Environment information.
    /// </summary>
    public EnvironmentInfo? Environment { get; set; }
}

using Validator.Core.Models.Enums;

namespace Validator.Core.Models.Results;

/// <summary>
/// Diagnostic information for a failure.
/// </summary>
public class FailureDiagnostic
{
    /// <summary>
    /// Step ID where the failure occurred.
    /// </summary>
    public int StepId { get; set; }

    /// <summary>
    /// Description of what was being attempted.
    /// </summary>
    public string? StepDescription { get; set; }

    /// <summary>
    /// Classification of the failure.
    /// </summary>
    public FailureClassification Classification { get; set; }

    /// <summary>
    /// Human-readable explanation of the failure.
    /// </summary>
    public required string Explanation { get; set; }

    /// <summary>
    /// Suggested fix (AI-assisted, best effort).
    /// </summary>
    public string? SuggestedFix { get; set; }

    /// <summary>
    /// Relevant error output.
    /// </summary>
    public string? ErrorOutput { get; set; }
}

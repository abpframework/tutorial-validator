using Validator.Core.Models;

namespace Validator.Analyst.Analysis;

/// <summary>
/// Result of step validation and normalization.
/// </summary>
public class StepValidationResult
{
    /// <summary>
    /// Validated and normalized steps.
    /// </summary>
    public List<TutorialStep> Steps { get; set; } = [];

    /// <summary>
    /// Issues found during validation.
    /// </summary>
    public List<string> Issues { get; set; } = [];

    /// <summary>
    /// Whether validation was successful (no critical issues).
    /// </summary>
    public bool IsValid => Steps.Count > 0 && Issues.All(i => !i.Contains("critical", StringComparison.OrdinalIgnoreCase));
}

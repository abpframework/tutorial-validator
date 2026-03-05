using Validator.Core.Models.Assertions;

namespace Validator.Core.Models.Steps;

/// <summary>
/// Represents an expectation/assertion step.
/// </summary>
public class ExpectationStep : TutorialStep
{
    /// <summary>
    /// List of assertions to validate.
    /// </summary>
    public required List<Assertion> Assertions { get; set; }
}

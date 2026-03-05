namespace Validator.Core.Models.Steps;

/// <summary>
/// Represents a code modification step.
/// AI-suggested changes that are applied by scripts.
/// </summary>
public class CodeChangeStep : TutorialStep
{
    /// <summary>
    /// Scope of the change: domain, application, infrastructure.
    /// </summary>
    public required string Scope { get; set; }

    /// <summary>
    /// Context information for the code change.
    /// </summary>
    public CodeChangeContext? InputContext { get; set; }

    /// <summary>
    /// Constraints for the code change.
    /// </summary>
    public CodeChangeConstraints? Constraints { get; set; }

    /// <summary>
    /// List of files expected to be created or modified.
    /// </summary>
    public List<string>? ExpectedFiles { get; set; }

    /// <summary>
    /// Specific code modifications to apply.
    /// </summary>
    public List<CodeModification>? Modifications { get; set; }
}

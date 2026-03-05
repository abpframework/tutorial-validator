namespace Validator.Core.Models.Steps;

/// <summary>
/// Constraints for code changes.
/// </summary>
public class CodeChangeConstraints
{
    /// <summary>
    /// If true, no new projects should be created.
    /// </summary>
    public bool NoNewProjects { get; set; }

    /// <summary>
    /// If true, no breaking changes should be introduced.
    /// </summary>
    public bool NoBreakingChanges { get; set; }
}

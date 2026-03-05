namespace Validator.Core.Models.Enums;

/// <summary>
/// Overall validation result status.
/// </summary>
public enum ValidationStatus
{
    /// <summary>
    /// All steps passed - tutorial works correctly.
    /// </summary>
    Passed,

    /// <summary>
    /// One or more steps failed - tutorial has issues.
    /// </summary>
    Failed
}

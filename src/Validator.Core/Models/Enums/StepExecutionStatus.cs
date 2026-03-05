namespace Validator.Core.Models.Enums;

/// <summary>
/// Status of a step execution.
/// </summary>
public enum StepExecutionStatus
{
    /// <summary>
    /// Step has not been executed yet.
    /// </summary>
    Pending,

    /// <summary>
    /// Step is currently executing.
    /// </summary>
    Running,

    /// <summary>
    /// Step completed successfully.
    /// </summary>
    Success,

    /// <summary>
    /// Step failed during execution.
    /// </summary>
    Failed,

    /// <summary>
    /// Step was skipped (e.g., due to previous failure).
    /// </summary>
    Skipped
}

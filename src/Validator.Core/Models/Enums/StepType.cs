namespace Validator.Core.Models.Enums;

/// <summary>
/// Defines the type of tutorial step to execute.
/// </summary>
public enum StepType
{
    /// <summary>
    /// CLI command execution (e.g., abp new, dotnet run).
    /// </summary>
    Command,

    /// <summary>
    /// File or directory operation (create, modify, delete).
    /// </summary>
    FileOperation,

    /// <summary>
    /// Code modification in existing files.
    /// </summary>
    CodeChange,

    /// <summary>
    /// Assertion or validation step.
    /// </summary>
    Expectation
}

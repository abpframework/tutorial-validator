using Validator.Core.Models.Enums;

namespace Validator.Core.Models.Steps;

/// <summary>
/// Represents a file or directory operation step.
/// </summary>
public class FileOperationStep : TutorialStep
{
    /// <summary>
    /// The type of operation: Create, Modify, or Delete.
    /// </summary>
    public FileOperationType Operation { get; set; }

    /// <summary>
    /// Path to the file or directory.
    /// </summary>
    public required string Path { get; set; }

    /// <summary>
    /// Entity type: "file" or "directory".
    /// </summary>
    public required string EntityType { get; set; }

    /// <summary>
    /// Content for create or modify operations (files only).
    /// </summary>
    public string? Content { get; set; }
}

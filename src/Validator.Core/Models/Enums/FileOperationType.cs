namespace Validator.Core.Models.Enums;

/// <summary>
/// Defines the type of file or directory operation.
/// </summary>
public enum FileOperationType
{
    /// <summary>
    /// Create a new file or directory.
    /// </summary>
    Create,

    /// <summary>
    /// Modify an existing file.
    /// </summary>
    Modify,

    /// <summary>
    /// Delete a file or directory.
    /// </summary>
    Delete
}

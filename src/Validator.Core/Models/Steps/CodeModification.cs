namespace Validator.Core.Models.Steps;

/// <summary>
/// Represents a specific code modification.
/// </summary>
public class CodeModification
{
    /// <summary>
    /// Path to the file being modified.
    /// </summary>
    public required string FilePath { get; set; }

    /// <summary>
    /// Complete file content (for full replacement).
    /// </summary>
    public string? FullContent { get; set; }

    /// <summary>
    /// Pattern to search for (for search-replace operations).
    /// </summary>
    public string? SearchPattern { get; set; }

    /// <summary>
    /// Replacement text for search-replace operations.
    /// </summary>
    public string? ReplaceWith { get; set; }
}

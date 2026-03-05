namespace Validator.Core.Models.Steps;

/// <summary>
/// Context information for code changes.
/// </summary>
public class CodeChangeContext
{
    /// <summary>
    /// Framework being used (e.g., "abp").
    /// </summary>
    public string? Framework { get; set; }

    /// <summary>
    /// UI framework: mvc, blazor, angular.
    /// </summary>
    public string? Ui { get; set; }

    /// <summary>
    /// Database type: ef, mongodb.
    /// </summary>
    public string? Database { get; set; }
}

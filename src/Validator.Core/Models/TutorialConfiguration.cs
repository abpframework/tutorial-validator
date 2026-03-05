namespace Validator.Core.Models;

/// <summary>
/// Configuration settings for the tutorial execution.
/// </summary>
public class TutorialConfiguration
{
    /// <summary>
    /// UI framework: mvc, blazor, angular.
    /// </summary>
    public required string Ui { get; set; }

    /// <summary>
    /// Database provider type: ef, mongodb.
    /// </summary>
    public required string Database { get; set; }

    /// <summary>
    /// Database provider: sqlserver, postgresql, mysql, sqlite.
    /// </summary>
    public string? DbProvider { get; set; }
}

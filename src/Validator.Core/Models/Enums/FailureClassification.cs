namespace Validator.Core.Models.Enums;

/// <summary>
/// Classification of why a step failed.
/// </summary>
public enum FailureClassification
{
    /// <summary>
    /// Documentation is outdated or incorrect.
    /// </summary>
    DocsOutdated,

    /// <summary>
    /// Template output has changed.
    /// </summary>
    TemplateChanged,

    /// <summary>
    /// Environment or tooling issue.
    /// </summary>
    EnvironmentIssue,

    /// <summary>
    /// CLI command changed or deprecated.
    /// </summary>
    CliChanged,

    /// <summary>
    /// Unknown or unclassified failure.
    /// </summary>
    Unknown
}

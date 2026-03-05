namespace Validator.Orchestrator.Models;

/// <summary>
/// Paths to output files.
/// </summary>
public class OutputFiles
{
    /// <summary>
    /// Path to the test plan file.
    /// </summary>
    public string? TestPlan { get; set; }

    /// <summary>
    /// Path to the validation result file.
    /// </summary>
    public string? ValidationResult { get; set; }

    /// <summary>
    /// Path to the validation report file.
    /// </summary>
    public string? ValidationReport { get; set; }

    /// <summary>
    /// Path to the analyst log file.
    /// </summary>
    public string? AnalystLog { get; set; }

    /// <summary>
    /// Path to the executor log file.
    /// </summary>
    public string? ExecutorLog { get; set; }

    /// <summary>
    /// Path to the Docker operations log file.
    /// </summary>
    public string? DockerLog { get; set; }

    /// <summary>
    /// Path to the generated project directory copied from the executor workspace.
    /// </summary>
    public string? GeneratedProject { get; set; }
}

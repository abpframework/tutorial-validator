namespace Validator.Orchestrator.Models;

/// <summary>
/// Result of running an external process.
/// </summary>
public class ProcessResult
{
    /// <summary>
    /// Exit code of the process.
    /// </summary>
    public int ExitCode { get; set; }

    /// <summary>
    /// Standard output from the process.
    /// </summary>
    public string Output { get; set; } = string.Empty;

    /// <summary>
    /// Standard error from the process.
    /// </summary>
    public string Error { get; set; } = string.Empty;

    /// <summary>
    /// Whether the process completed successfully (exit code 0).
    /// </summary>
    public bool Success => ExitCode == 0;

    /// <summary>
    /// Duration of the process execution.
    /// </summary>
    public TimeSpan Duration { get; set; }
}

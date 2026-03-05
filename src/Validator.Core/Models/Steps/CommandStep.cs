namespace Validator.Core.Models.Steps;

/// <summary>
/// Represents a CLI command execution step.
/// </summary>
public class CommandStep : TutorialStep
{
    /// <summary>
    /// The command to execute (e.g., "abp new BookStore -u mvc").
    /// </summary>
    public required string Command { get; set; }

    /// <summary>
    /// Expected outcomes of the command execution.
    /// </summary>
    public CommandExpectation? Expects { get; set; }

    /// <summary>
    /// Whether this command starts a long-running process (e.g., web server).
    /// When true, the executor starts the process in the background and waits for
    /// the <see cref="ReadinessPattern"/> instead of waiting for the process to exit.
    /// </summary>
    public bool IsLongRunning { get; set; }

    /// <summary>
    /// Regex pattern to match in stdout that indicates the process is ready.
    /// For example, <c>"Now listening on"</c> for ASP.NET Core web applications.
    /// Only used when <see cref="IsLongRunning"/> is <c>true</c>.
    /// </summary>
    public string? ReadinessPattern { get; set; }

    /// <summary>
    /// Timeout in seconds to wait for the <see cref="ReadinessPattern"/>. Defaults to 60.
    /// </summary>
    public int ReadinessTimeoutSeconds { get; set; } = 60;
}

using Validator.Core.Models.Enums;

namespace Validator.Orchestrator.Models;

/// <summary>
/// Summary of the orchestration run.
/// </summary>
public class OrchestrationSummary
{
    /// <summary>
    /// URL of the tutorial that was validated.
    /// </summary>
    public string? TutorialUrl { get; set; }

    /// <summary>
    /// Name of the tutorial.
    /// </summary>
    public string? TutorialName { get; set; }

    /// <summary>
    /// When the orchestration started.
    /// </summary>
    public DateTime StartedAt { get; set; }

    /// <summary>
    /// When the orchestration completed.
    /// </summary>
    public DateTime CompletedAt { get; set; }

    /// <summary>
    /// Duration of the analyst phase.
    /// </summary>
    public TimeSpan? AnalystDuration { get; set; }

    /// <summary>
    /// Duration of the executor/Docker phase.
    /// </summary>
    public TimeSpan? ExecutorDuration { get; set; }

    /// <summary>
    /// Duration of the output organization phase.
    /// </summary>
    public TimeSpan? OrganizeDuration { get; set; }

    /// <summary>
    /// Duration of the email report phase.
    /// </summary>
    public TimeSpan? EmailDuration { get; set; }

    /// <summary>
    /// Whether an email report was sent.
    /// </summary>
    public bool EmailSent { get; set; }

    /// <summary>
    /// Duration of the Discord report phase.
    /// </summary>
    public TimeSpan? DiscordDuration { get; set; }

    /// <summary>
    /// Whether a Discord notification was sent.
    /// </summary>
    public bool DiscordSent { get; set; }

    /// <summary>
    /// Total duration of the orchestration.
    /// </summary>
    public TimeSpan Duration => CompletedAt - StartedAt;

    /// <summary>
    /// Whether the analyst phase succeeded.
    /// </summary>
    public bool AnalystSuccess { get; set; }

    /// <summary>
    /// Error message from analyst if it failed.
    /// </summary>
    public string? AnalystError { get; set; }

    /// <summary>
    /// Whether the executor phase succeeded.
    /// </summary>
    public bool ExecutorSuccess { get; set; }

    /// <summary>
    /// Error message from executor if it failed.
    /// </summary>
    public string? ExecutorError { get; set; }

    /// <summary>
    /// Overall validation status.
    /// </summary>
    public ValidationStatus OverallStatus { get; set; }

    /// <summary>
    /// Paths to output files.
    /// </summary>
    public OutputFiles Files { get; set; } = new();

    /// <summary>
    /// Environment information.
    /// </summary>
    public OrchestratorEnvironmentInfo Environment { get; set; } = new();
}

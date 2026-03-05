namespace Validator.Orchestrator.Models;

/// <summary>
/// Options for the orchestrator run command.
/// </summary>
public class OrchestratorOptions
{
    /// <summary>
    /// URL of the tutorial to validate.
    /// </summary>
    public string? TutorialUrl { get; set; }

    /// <summary>
    /// Path to an existing testplan.json file (skips analyst).
    /// </summary>
    public string? TestPlanPath { get; set; }

    /// <summary>
    /// Output directory for all results.
    /// </summary>
    public string OutputPath { get; set; } = "./output";

    /// <summary>
    /// Skip the analyst phase and use existing testplan.json.
    /// </summary>
    public bool SkipAnalyst { get; set; }

    /// <summary>
    /// Keep Docker containers running after execution.
    /// </summary>
    public bool KeepContainers { get; set; }

    /// <summary>
    /// Path to the AI configuration file.
    /// </summary>
    public string? ConfigPath { get; set; }

    /// <summary>
    /// Timeout in minutes for the entire orchestration.
    /// </summary>
    public int TimeoutMinutes { get; set; } = 120;

    /// <summary>
    /// Skip Docker and run executor locally (for debugging).
    /// </summary>
    public bool LocalExecution { get; set; }

    /// <summary>
    /// Developer persona for the executor agent (junior, mid, senior).
    /// Defaults to "mid".
    /// </summary>
    public string Persona { get; set; } = "mid";
}

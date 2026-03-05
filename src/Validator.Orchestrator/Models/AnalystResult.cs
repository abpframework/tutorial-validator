namespace Validator.Orchestrator.Models;

/// <summary>
/// Result from running the Analyst.
/// </summary>
public class AnalystResult
{
    public bool Success { get; set; }
    public string? TestPlanPath { get; set; }
    public string? TutorialName { get; set; }
    public string? Error { get; set; }
    public string? LogPath { get; set; }
}

namespace Validator.Core.Models;

/// <summary>
/// Root container for the tutorial test plan.
/// This is the contract between the Analyst (generates) and Executor (consumes).
/// </summary>
public class TestPlan
{
    /// <summary>
    /// Name of the tutorial being validated.
    /// </summary>
    public required string TutorialName { get; set; }

    /// <summary>
    /// URL of the tutorial documentation.
    /// </summary>
    public required string TutorialUrl { get; set; }

    /// <summary>
    /// ABP Framework version being tested.
    /// </summary>
    public required string AbpVersion { get; set; }

    /// <summary>
    /// Tutorial configuration (UI, database, etc.).
    /// </summary>
    public required TutorialConfiguration Configuration { get; set; }

    /// <summary>
    /// Ordered list of steps to execute.
    /// </summary>
    public required List<TutorialStep> Steps { get; set; }
}

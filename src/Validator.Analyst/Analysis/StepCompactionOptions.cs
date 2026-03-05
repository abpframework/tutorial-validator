namespace Validator.Analyst.Analysis;

/// <summary>
/// Configuration for post-extraction step compaction.
/// </summary>
public class StepCompactionOptions
{
    /// <summary>
    /// Whether compaction is enabled.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Target step count to aim for.
    /// </summary>
    public int TargetStepCount { get; set; } = 50;

    /// <summary>
    /// Maximum step count before aggressive fallback rules are applied.
    /// </summary>
    public int MaxStepCount { get; set; } = 55;

    /// <summary>
    /// Maximum number of code modifications to merge into one code_change step.
    /// </summary>
    public int MaxCodeModificationsPerStep { get; set; } = 8;
}


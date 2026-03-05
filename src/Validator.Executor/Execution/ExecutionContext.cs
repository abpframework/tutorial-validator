using Validator.Core.Models;
using Validator.Core.Models.Results;

namespace Validator.Executor.Execution;

/// <summary>
/// Holds shared state during tutorial execution.
/// </summary>
public class ExecutionContext
{
    /// <summary>
    /// The root working directory for the tutorial project.
    /// All relative paths are resolved from this directory.
    /// </summary>
    public required string WorkingDirectory { get; init; }

    /// <summary>
    /// The TestPlan being executed.
    /// </summary>
    public required TestPlan TestPlan { get; init; }

    /// <summary>
    /// Environment information collected at execution start.
    /// </summary>
    public EnvironmentInfo Environment { get; init; } = new();

    /// <summary>
    /// When execution started.
    /// </summary>
    public DateTime StartedAt { get; init; } = DateTime.UtcNow;

    /// <summary>
    /// The current step being executed (for logging/diagnostics).
    /// </summary>
    public int? CurrentStepId { get; set; }

    /// <summary>
    /// Whether this is a dry-run (validate only, no actual execution).
    /// </summary>
    public bool DryRun { get; init; }

    /// <summary>
    /// Resolves a path relative to the working directory.
    /// Handles "root" as a special case meaning the working directory itself.
    /// </summary>
    /// <param name="relativePath">The relative path or "root".</param>
    /// <returns>The absolute path.</returns>
    public string ResolvePath(string relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath) || 
            relativePath.Equals("root", StringComparison.OrdinalIgnoreCase))
        {
            return WorkingDirectory;
        }

        // Normalize path separators
        var normalizedPath = relativePath.Replace('\\', Path.DirectorySeparatorChar)
                                          .Replace('/', Path.DirectorySeparatorChar);

        return Path.GetFullPath(Path.Combine(WorkingDirectory, normalizedPath));
    }

    /// <summary>
    /// Collects environment information from the current system.
    /// </summary>
    public static EnvironmentInfo CollectEnvironmentInfo()
    {
        return new EnvironmentInfo
        {
            OperatingSystem = System.Environment.OSVersion.ToString(),
            DotNetVersion = System.Environment.Version.ToString(),
            MachineName = System.Environment.MachineName,
            // NodeVersion and AbpCliVersion will be collected by command execution later
            NodeVersion = null,
            AbpCliVersion = null
        };
    }
}

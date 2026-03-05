namespace Validator.Orchestrator.Models;

/// <summary>
/// Environment information for the orchestration.
/// Named <c>OrchestratorEnvironmentInfo</c> to avoid conflict with
/// <see cref="Validator.Core.Models.Results.EnvironmentInfo"/>.
/// </summary>
public class OrchestratorEnvironmentInfo
{
    /// <summary>
    /// Operating system.
    /// </summary>
    public string? OperatingSystem { get; set; }

    /// <summary>
    /// .NET SDK version on host.
    /// </summary>
    public string? DotNetVersion { get; set; }

    /// <summary>
    /// Docker version.
    /// </summary>
    public string? DockerVersion { get; set; }

    /// <summary>
    /// Machine name.
    /// </summary>
    public string? MachineName { get; set; }
}

namespace Validator.Core.Models.Results;

/// <summary>
/// Information about the execution environment.
/// </summary>
public class EnvironmentInfo
{
    /// <summary>
    /// Operating system.
    /// </summary>
    public string? OperatingSystem { get; set; }

    /// <summary>
    /// .NET SDK version.
    /// </summary>
    public string? DotNetVersion { get; set; }

    /// <summary>
    /// Node.js version.
    /// </summary>
    public string? NodeVersion { get; set; }

    /// <summary>
    /// ABP CLI version.
    /// </summary>
    public string? AbpCliVersion { get; set; }

    /// <summary>
    /// Machine name or container ID.
    /// </summary>
    public string? MachineName { get; set; }
}

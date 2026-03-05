namespace Validator.Core.Models.Assertions;

/// <summary>
/// Assertion for build success validation.
/// </summary>
public class BuildAssertion : Assertion
{
    /// <summary>
    /// Build command to execute. Defaults to "dotnet build".
    /// </summary>
    public string Command { get; set; } = "dotnet build";

    /// <summary>
    /// Expected exit code. Defaults to 0 (success).
    /// </summary>
    public int ExpectsExitCode { get; set; } = 0;
}

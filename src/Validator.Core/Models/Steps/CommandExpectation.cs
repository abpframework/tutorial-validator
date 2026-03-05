namespace Validator.Core.Models.Steps;

/// <summary>
/// Expected outcomes of a command execution.
/// </summary>
public class CommandExpectation
{
    /// <summary>
    /// Expected exit code. Defaults to 0 (success).
    /// </summary>
    public int ExitCode { get; set; } = 0;

    /// <summary>
    /// List of files or directories expected to be created.
    /// </summary>
    public List<string>? Creates { get; set; }
}

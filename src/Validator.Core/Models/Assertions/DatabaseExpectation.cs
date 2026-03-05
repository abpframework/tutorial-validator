namespace Validator.Core.Models.Assertions;

/// <summary>
/// Expected database state.
/// </summary>
public class DatabaseExpectation
{
    /// <summary>
    /// Whether migrations should be applied.
    /// </summary>
    public bool MigrationsApplied { get; set; }

    /// <summary>
    /// List of tables that should exist.
    /// </summary>
    public List<string>? TablesExist { get; set; }
}

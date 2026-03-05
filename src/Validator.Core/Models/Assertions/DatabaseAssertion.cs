namespace Validator.Core.Models.Assertions;

/// <summary>
/// Assertion for database state validation.
/// </summary>
public class DatabaseAssertion : Assertion
{
    /// <summary>
    /// Database provider: ef, mongodb.
    /// </summary>
    public required string Provider { get; set; }

    /// <summary>
    /// Expected database state.
    /// </summary>
    public required DatabaseExpectation Expects { get; set; }
}

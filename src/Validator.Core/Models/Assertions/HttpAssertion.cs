namespace Validator.Core.Models.Assertions;

/// <summary>
/// Assertion for HTTP endpoint validation.
/// </summary>
public class HttpAssertion : Assertion
{
    /// <summary>
    /// URL to request.
    /// </summary>
    public required string Url { get; set; }

    /// <summary>
    /// HTTP method. Defaults to "GET".
    /// </summary>
    public string Method { get; set; } = "GET";

    /// <summary>
    /// Expected HTTP status code. Defaults to 200.
    /// </summary>
    public int ExpectsStatus { get; set; } = 200;

    /// <summary>
    /// Optional expected content in the response body.
    /// </summary>
    public string? ExpectsContent { get; set; }
}

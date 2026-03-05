namespace Validator.Reporter.Email;

/// <summary>
/// Represents an email message to be sent.
/// </summary>
public class EmailMessage
{
    /// <summary>
    /// Email subject line.
    /// </summary>
    public required string Subject { get; set; }

    /// <summary>
    /// HTML body content.
    /// </summary>
    public required string HtmlBody { get; set; }

    /// <summary>
    /// From email address.
    /// </summary>
    public required string FromAddress { get; set; }

    /// <summary>
    /// From display name.
    /// </summary>
    public required string FromName { get; set; }

    /// <summary>
    /// To email addresses.
    /// </summary>
    public required List<string> ToAddresses { get; set; }

    /// <summary>
    /// CC email addresses (optional).
    /// </summary>
    public List<string>? CcAddresses { get; set; }
}

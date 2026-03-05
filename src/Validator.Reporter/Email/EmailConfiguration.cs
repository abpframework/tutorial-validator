namespace Validator.Reporter.Email;

/// <summary>
/// Configuration for SMTP email sending.
/// </summary>
public class EmailConfiguration
{
    /// <summary>
    /// Whether email notifications are enabled.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// SMTP server host.
    /// </summary>
    public required string SmtpHost { get; set; }

    /// <summary>
    /// SMTP server port (default: 587 for TLS).
    /// </summary>
    public int SmtpPort { get; set; } = 587;

    /// <summary>
    /// Whether to use SSL/TLS.
    /// </summary>
    public bool UseSsl { get; set; } = true;

    /// <summary>
    /// SMTP username for authentication (optional).
    /// </summary>
    public string? Username { get; set; }

    /// <summary>
    /// SMTP password for authentication (optional).
    /// </summary>
    public string? Password { get; set; }

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

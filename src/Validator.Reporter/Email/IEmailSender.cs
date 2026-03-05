namespace Validator.Reporter.Email;

/// <summary>
/// Interface for sending email messages.
/// </summary>
public interface IEmailSender
{
    /// <summary>
    /// Sends an email message asynchronously.
    /// </summary>
    /// <param name="message">The email message to send.</param>
    /// <param name="config">SMTP configuration.</param>
    Task SendAsync(EmailMessage message, EmailConfiguration config);
}

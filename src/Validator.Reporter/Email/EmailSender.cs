using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;

namespace Validator.Reporter.Email;

/// <summary>
/// Email sender implementation using MailKit.
/// </summary>
public class EmailSender : IEmailSender
{
    private const int DefaultTimeoutSeconds = 30;

    /// <summary>
    /// Sends an email message asynchronously.
    /// </summary>
    /// <param name="message">The email message to send.</param>
    /// <param name="config">SMTP configuration.</param>
    public async Task SendAsync(EmailMessage message, EmailConfiguration config)
    {
        var mimeMessage = BuildMimeMessage(message);

        using var client = new SmtpClient
        {
            Timeout = DefaultTimeoutSeconds * 1000
        };

        try
        {
            // Connect to SMTP server
            var secureSocketOptions = config.UseSsl 
                ? SecureSocketOptions.StartTls 
                : SecureSocketOptions.None;

            await client.ConnectAsync(config.SmtpHost, config.SmtpPort, secureSocketOptions);

            // Authenticate if credentials provided
            if (!string.IsNullOrEmpty(config.Username) && !string.IsNullOrEmpty(config.Password))
            {
                await client.AuthenticateAsync(config.Username, config.Password);
            }

            // Send the message
            await client.SendAsync(mimeMessage);

            // Disconnect
            await client.DisconnectAsync(true);
        }
        catch (Exception ex)
        {
            throw new EmailSendException($"Failed to send email: {ex.Message}", ex);
        }
    }

    private static MimeMessage BuildMimeMessage(EmailMessage message)
    {
        var mimeMessage = new MimeMessage();

        // From
        mimeMessage.From.Add(new MailboxAddress(message.FromName, message.FromAddress));

        // To
        foreach (var toAddress in message.ToAddresses)
        {
            mimeMessage.To.Add(MailboxAddress.Parse(toAddress));
        }

        // CC
        if (message.CcAddresses != null)
        {
            foreach (var ccAddress in message.CcAddresses)
            {
                mimeMessage.Cc.Add(MailboxAddress.Parse(ccAddress));
            }
        }

        // Subject
        mimeMessage.Subject = message.Subject;

        // Body
        var bodyBuilder = new BodyBuilder
        {
            HtmlBody = message.HtmlBody
        };
        mimeMessage.Body = bodyBuilder.ToMessageBody();

        return mimeMessage;
    }
}

/// <summary>
/// Exception thrown when email sending fails.
/// </summary>
public class EmailSendException : Exception
{
    public EmailSendException(string message) : base(message)
    {
    }

    public EmailSendException(string message, Exception innerException) : base(message, innerException)
    {
    }
}

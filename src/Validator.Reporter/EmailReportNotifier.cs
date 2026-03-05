using Validator.Core.Models.Results;
using Validator.Reporter.Email;
using Validator.Reporter.Formatting;

namespace Validator.Reporter;

/// <summary>
/// Sends validation report notifications via email.
/// </summary>
public class EmailReportNotifier : IReportNotifier
{
    private readonly IEmailSender _emailSender;
    private readonly HtmlReportFormatter _htmlFormatter;
    private readonly EmailConfiguration _config;

    /// <summary>
    /// Initializes a new instance of the EmailReportNotifier class.
    /// </summary>
    /// <param name="config">Email configuration.</param>
    public EmailReportNotifier(EmailConfiguration config) : this(new EmailSender(), new HtmlReportFormatter(), config)
    {
    }

    /// <summary>
    /// Initializes a new instance of the EmailReportNotifier class with custom dependencies.
    /// </summary>
    /// <param name="emailSender">The email sender implementation.</param>
    /// <param name="htmlFormatter">The HTML formatter implementation.</param>
    /// <param name="config">Email configuration.</param>
    public EmailReportNotifier(IEmailSender emailSender, HtmlReportFormatter htmlFormatter, EmailConfiguration config)
    {
        _emailSender = emailSender;
        _htmlFormatter = htmlFormatter;
        _config = config;
    }

    /// <summary>
    /// Sends a validation report via email.
    /// </summary>
    /// <param name="report">The validation report to send.</param>
    public async Task SendReportAsync(ValidationReport report)
    {
        if (!_config.Enabled)
        {
            Console.WriteLine("Email reporting is disabled.");
            return;
        }

        Console.WriteLine("Preparing email report...");

        var htmlBody = _htmlFormatter.FormatAsHtml(report);

        var emailMessage = new EmailMessage
        {
            Subject = report.Subject,
            HtmlBody = htmlBody,
            FromAddress = _config.FromAddress,
            FromName = _config.FromName,
            ToAddresses = _config.ToAddresses,
            CcAddresses = _config.CcAddresses
        };

        Console.WriteLine($"Sending email to {string.Join(", ", _config.ToAddresses)}...");

        try
        {
            await _emailSender.SendAsync(emailMessage, _config);
            Console.WriteLine("Email sent successfully.");
        }
        catch (EmailSendException ex)
        {
            Console.WriteLine($"Failed to send email: {ex.Message}");
            throw;
        }
    }
}

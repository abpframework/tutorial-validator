using Validator.Core.Models.Results;
using Validator.Reporter.Discord;
using Validator.Reporter.Formatting;

namespace Validator.Reporter;

/// <summary>
/// Sends validation report notifications via a Discord webhook.
/// </summary>
public class DiscordReportNotifier : IReportNotifier
{
    private readonly IDiscordSender _discordSender;
    private readonly DiscordReportFormatter _formatter;
    private readonly DiscordConfiguration _config;

    /// <summary>
    /// Initializes a new instance of the DiscordReportNotifier class.
    /// </summary>
    /// <param name="config">Discord webhook configuration.</param>
    public DiscordReportNotifier(DiscordConfiguration config) : this(new DiscordSender(), new DiscordReportFormatter(), config)
    {
    }

    /// <summary>
    /// Initializes a new instance of the DiscordReportNotifier class with custom dependencies.
    /// </summary>
    /// <param name="discordSender">The Discord sender implementation.</param>
    /// <param name="formatter">The Discord message formatter.</param>
    /// <param name="config">Discord webhook configuration.</param>
    public DiscordReportNotifier(IDiscordSender discordSender, DiscordReportFormatter formatter, DiscordConfiguration config)
    {
        _discordSender = discordSender;
        _formatter = formatter;
        _config = config;
    }

    /// <summary>
    /// Sends a validation report via Discord webhook.
    /// </summary>
    /// <param name="report">The validation report to send.</param>
    public async Task SendReportAsync(ValidationReport report)
    {
        if (!_config.Enabled)
        {
            Console.WriteLine("Discord reporting is disabled.");
            return;
        }

        Console.WriteLine("Preparing Discord report...");

        var messages = _formatter.FormatMessages(report);
        Console.WriteLine($"Sending Discord notification in {messages.Count} message(s)...");

        try
        {
            for (var i = 0; i < messages.Count; i++)
            {
                Console.WriteLine($"Sending Discord message {i + 1}/{messages.Count}...");
                await _discordSender.SendAsync(messages[i], _config);
            }

            Console.WriteLine("Discord notification sent successfully.");
        }
        catch (DiscordSendException ex)
        {
            Console.WriteLine($"Failed to send Discord notification: {ex.Message}");
            throw;
        }
    }
}

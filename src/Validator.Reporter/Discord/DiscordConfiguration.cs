namespace Validator.Reporter.Discord;

/// <summary>
/// Configuration for Discord webhook notifications.
/// </summary>
public class DiscordConfiguration
{
    /// <summary>
    /// Whether Discord notifications are enabled.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Discord webhook URL.
    /// </summary>
    public required string WebhookUrl { get; set; }
}

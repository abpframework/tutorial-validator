namespace Validator.Reporter.Discord;

/// <summary>
/// Interface for sending Discord webhook messages.
/// </summary>
public interface IDiscordSender
{
    /// <summary>
    /// Sends a Discord message via webhook asynchronously.
    /// </summary>
    /// <param name="message">The message to send.</param>
    /// <param name="config">Discord webhook configuration.</param>
    Task SendAsync(DiscordMessage message, DiscordConfiguration config);
}

namespace Validator.Reporter.Discord;

/// <summary>
/// Exception thrown when a Discord webhook message fails to send.
/// </summary>
public class DiscordSendException : Exception
{
    public DiscordSendException(string message) : base(message)
    {
    }

    public DiscordSendException(string message, Exception innerException) : base(message, innerException)
    {
    }
}

namespace Validator.Reporter.Discord;

/// <summary>
/// Represents a Discord webhook message using the embeds API.
/// </summary>
public class DiscordMessage
{
    /// <summary>
    /// Optional plain-text content shown above the embed.
    /// </summary>
    public string? Content { get; set; }

    /// <summary>
    /// Embed objects to include in the message.
    /// </summary>
    public List<DiscordEmbed> Embeds { get; set; } = [];
}

/// <summary>
/// A Discord rich embed card.
/// </summary>
public class DiscordEmbed
{
    /// <summary>
    /// Title of the embed.
    /// </summary>
    public string? Title { get; set; }

    /// <summary>
    /// Description shown beneath the title.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Decimal color value for the left border stripe.
    /// </summary>
    public int Color { get; set; }

    /// <summary>
    /// Fields displayed in the embed body.
    /// </summary>
    public List<DiscordEmbedField> Fields { get; set; } = [];

    /// <summary>
    /// Footer text shown at the bottom of the embed.
    /// </summary>
    public DiscordEmbedFooter? Footer { get; set; }

    /// <summary>
    /// ISO 8601 timestamp shown in the footer.
    /// </summary>
    public string? Timestamp { get; set; }
}

/// <summary>
/// A name/value field inside a Discord embed.
/// </summary>
public class DiscordEmbedField
{
    /// <summary>
    /// Field label.
    /// </summary>
    public required string Name { get; set; }

    /// <summary>
    /// Field value.
    /// </summary>
    public required string Value { get; set; }

    /// <summary>
    /// Whether the field should be displayed inline.
    /// </summary>
    public bool Inline { get; set; }
}

/// <summary>
/// Footer of a Discord embed.
/// </summary>
public class DiscordEmbedFooter
{
    /// <summary>
    /// Footer text.
    /// </summary>
    public required string Text { get; set; }
}

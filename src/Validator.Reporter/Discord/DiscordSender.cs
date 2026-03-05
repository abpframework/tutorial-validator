using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Validator.Reporter.Discord;

/// <summary>
/// Sends Discord webhook messages using HttpClient.
/// </summary>
public class DiscordSender : IDiscordSender
{
    private static readonly HttpClient HttpClient = new()
    {
        Timeout = TimeSpan.FromSeconds(30)
    };

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    /// <summary>
    /// Sends a Discord message via webhook asynchronously.
    /// </summary>
    /// <param name="message">The message to send.</param>
    /// <param name="config">Discord webhook configuration.</param>
    public async Task SendAsync(DiscordMessage message, DiscordConfiguration config)
    {
        if (string.IsNullOrWhiteSpace(config.WebhookUrl))
        {
            throw new DiscordSendException("Discord webhook URL is not configured.");
        }

        try
        {
            var response = await HttpClient.PostAsJsonAsync(config.WebhookUrl, message, JsonOptions);

            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync();
                throw new DiscordSendException(
                    $"Discord webhook returned {(int)response.StatusCode} {response.StatusCode}. Response: {body}");
            }
        }
        catch (DiscordSendException)
        {
            throw;
        }
        catch (HttpRequestException ex)
        {
            throw new DiscordSendException($"HTTP request to Discord webhook failed: {ex.Message}", ex);
        }
        catch (TaskCanceledException ex)
        {
            throw new DiscordSendException($"Discord webhook request timed out: {ex.Message}", ex);
        }
        catch (Exception ex)
        {
            throw new DiscordSendException($"Failed to send Discord message: {ex.Message}", ex);
        }
    }
}
